namespace MakroPad.Core.Configuration;

public sealed record MakroPadConfig
{
    public int Version { get; init; } = 1;

    public HardwareConfig Hardware { get; init; } = new();

    public string ActiveProfile { get; init; } = "Default";

    public List<ProfileConfig> Profiles { get; init; } = [];

    public static MakroPadConfig CreateDefault()
    {
        return new MakroPadConfig
        {
            Hardware = new HardwareConfig
            {
                Buttons =
                [
                    new ButtonHardwareConfig { Id = 1, Pin = 4, Name = "Button 1" }
                ],
                Sliders =
                [
                    new SliderHardwareConfig { Id = 1, Pin = 1, Name = "Slider 1" }
                ],
                Display = new DisplayHardwareConfig()
            },
            ActiveProfile = "Default",
            Profiles =
            [
                new ProfileConfig
                {
                    Name = "Default",
                    Bindings =
                    [
                        new ActionBinding
                        {
                            Input = "button:1",
                            Action = "media.playPause"
                        },
                        new ActionBinding
                        {
                            Input = "slider:1",
                            Action = "system.volume"
                        }
                    ]
                }
            ]
        };
    }
}
