namespace MakroPad.Core.Configuration;

public enum ProfileConfigErrorCode
{
    ProfileNameRequired,
    ProfileNameContainsReservedCharacter,
    ProfileAlreadyExists,
    ActiveProfileNotFound,
    AtLeastOneProfileRequired,
    UpdatedProfileNameMismatch
}
