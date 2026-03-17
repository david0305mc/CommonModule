using UnityEngine;

public static class UserDataMapper
{
    public static UserDataDto ToDto(this UserData runtime)
    {
        var dto = new UserDataDto
        {
        };
        return dto;
    }
    public static UserData FromDto(this UserDataDto dto)
    {
        var runtime = new UserData();
        return runtime;
    }
}
