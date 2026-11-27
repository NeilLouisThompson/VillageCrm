using VillageCrm.Models;

namespace VillageCrm.Services;

/// <summary>
/// Holds who the user is "viewing as". This is a mock stand-in for real
/// sign-in. In the real system the role comes from the logged-in account.
/// </summary>
public class ViewerContext
{
    public UserRole Role { get; private set; } = UserRole.Reception;
    public string Name { get; private set; } = "";
    public int? ResidentId { get; private set; }
    public bool BiggerText { get; private set; }

    public bool IsResident => Role == UserRole.Resident;

    public event Action? Changed;

    public void SetStaff(UserRole role, string name)
    {
        Role = role;
        Name = name;
        ResidentId = null;
        Changed?.Invoke();
    }

    public void SetResident(int residentId, string name)
    {
        Role = UserRole.Resident;
        Name = name;
        ResidentId = residentId;
        Changed?.Invoke();
    }

    public void ToggleBiggerText()
    {
        BiggerText = !BiggerText;
        Changed?.Invoke();
    }
}
