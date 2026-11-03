#include "ble.h"

#include <Arduino.h>
#include <BLE2902.h>
#include <BLEDevice.h>
#include <BLESecurity.h>
#include <BLEServer.h>

#include <atomic>

#include "config.h"
#include "display.h"

namespace ble {

namespace {

// Nordic UART Service: the app writes to RX, the device notifies on TX.
constexpr const char* ServiceUuid = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
constexpr const char* RxUuid = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";
constexpr const char* TxUuid = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";
constexpr esp_gatt_perm_t EncryptedReadWrite =
    static_cast<esp_gatt_perm_t>(ESP_GATT_PERM_READ_ENC_MITM | ESP_GATT_PERM_WRITE_ENC_MITM);

BLEServer* server = nullptr;
BLECharacteristic* tx = nullptr;
BLE2902* txConfig = nullptr;
QueueHandle_t rxQueue = nullptr;
std::atomic<bool> clientConnected{false};
std::atomic<uint16_t> connId{0};

// Security callbacks run on the Bluetooth task; the main loop renders their results.
std::atomic<bool> passkeyPending{false};
std::atomic<uint32_t> passkey{0};
std::atomic<int> pairingResult{0};  // 0 none, 1 ok, -1 failed

class ServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer*, esp_ble_gatts_cb_param_t* param) override {
    connId = param->connect.conn_id;
    clientConnected = true;
  }

  void onDisconnect(BLEServer* s) override {
    clientConnected = false;
    s->startAdvertising();
  }
};

class RxCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic* characteristic) override {
    const std::string value = characteristic->getValue();
    for (const char c : value) {
      xQueueSend(rxQueue, &c, 0);  // drops bytes when full; the line buffer then rejects the line
    }
  }
};

class SecurityCallbacks : public BLESecurityCallbacks {
  uint32_t onPassKeyRequest() override { return 0; }  // never asked: the device can only display

  void onPassKeyNotify(uint32_t key) override {
    passkey = key;
    passkeyPending = true;
  }

  bool onSecurityRequest() override { return true; }

  void onAuthenticationComplete(esp_ble_auth_cmpl_t result) override { pairingResult = result.success ? 1 : -1; }

  bool onConfirmPIN(uint32_t) override { return false; }
};

}  // namespace

void setup() {
  rxQueue = xQueueCreate(config::BleRxQueueBytes, sizeof(char));

  BLEDevice::init(config::BleDeviceName);
  BLEDevice::setMTU(config::BleMtu);
  BLEDevice::setEncryptionLevel(ESP_BLE_SEC_ENCRYPT_MITM);
  BLEDevice::setSecurityCallbacks(new SecurityCallbacks());

  BLESecurity* security = new BLESecurity();
  security->setAuthenticationMode(ESP_LE_AUTH_REQ_SC_MITM_BOND);
  security->setCapability(ESP_IO_CAP_OUT);
  security->setInitEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);
  security->setRespEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);

  server = BLEDevice::createServer();
  server->setCallbacks(new ServerCallbacks());
  BLEService* service = server->createService(ServiceUuid);

  tx = service->createCharacteristic(TxUuid, BLECharacteristic::PROPERTY_NOTIFY);
  tx->setAccessPermissions(EncryptedReadWrite);
  txConfig = new BLE2902();
  // Subscribing requires an encrypted, bonded link, so nothing reaches an unpaired client.
  txConfig->setAccessPermissions(EncryptedReadWrite);
  tx->addDescriptor(txConfig);

  BLECharacteristic* rx = service->createCharacteristic(
      RxUuid, BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR);
  rx->setAccessPermissions(EncryptedReadWrite);
  rx->setCallbacks(new RxCallbacks());

  service->start();
  BLEAdvertising* advertising = BLEDevice::getAdvertising();
  advertising->addServiceUUID(ServiceUuid);
  advertising->setScanResponse(true);
  BLEDevice::startAdvertising();
}

void poll(uint32_t nowMs) {
  if (passkeyPending.exchange(false)) {
    char text[16];
    snprintf(text, sizeof(text), "Code %06lu", static_cast<unsigned long>(passkey.load()));
    display::showStatus("Bluetooth", text, display::Icon::Sync, nowMs, config::BlePasskeyShowMs);
  }
  const int result = pairingResult.exchange(0);
  if (result != 0) {
    display::showStatus("Bluetooth", result > 0 ? "Gekoppelt" : "Fehlgeschlagen",
                        result > 0 ? display::Icon::Ok : display::Icon::Fail, nowMs);
  }
}

bool connected() {
  return clientConnected && txConfig->getNotifications();
}

bool read(char& c) {
  return xQueueReceive(rxQueue, &c, 0) == pdTRUE;
}

void write(const std::string& data) {
  if (!connected()) {
    return;
  }
  // A notification carries at most MTU - 3 bytes; the app reassembles lines at '\n'.
  const uint16_t mtu = server->getPeerMTU(connId);
  const size_t chunk = (mtu > 23 ? mtu : 23) - 3;
  for (size_t i = 0; i < data.size(); i += chunk) {
    tx->setValue(data.substr(i, chunk));
    tx->notify();
  }
}

}  // namespace ble
