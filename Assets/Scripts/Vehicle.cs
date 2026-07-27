using UnityEngine;

public abstract class Vehicle:MonoBehaviour
{
    #region Enum
    public enum VehicleType {Car};
    #endregion
    #region Fields
    public VehicleType vehicleType;
    #endregion
}
