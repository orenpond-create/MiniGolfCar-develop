using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class GameSettings : MonoBehaviour
{
    #region Fields
    static GameSettings instance;
    [SerializeField] FollowArrow followArrow;
    #endregion

    #region Properties
    public static GameSettings Instance
    {
        get { return instance; }
    }
    public FollowArrow UIArrow
    {
        get { return followArrow; }
    }
    #endregion

    #region Methods
    public void Awake()
    {
        instance = this;
        if (followArrow == null)
            Debug.LogError("You need to assign followArrow");
    }

    #endregion
}
