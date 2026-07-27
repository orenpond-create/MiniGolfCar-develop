using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : Player
{
    #region Fields
    bool selected = false;
    Rigidbody myRigidBody;
    Vector3 startMousePos;
    [SerializeField] float ballSpeed = 4;
    Camera myMainCamera;
    #endregion

    #region Methods
    private void Awake()
    {
        myRigidBody = GetComponentInChildren<Rigidbody>();

        if (myRigidBody == null)
            Debug.LogError("Missing Rigidbody Component");

        myMainCamera = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!selected)
            {
                Player tempPlayer = getPlayer();
                if(tempPlayer != null)
                {
                    if((tempPlayer as Ball) == this)
                    {
                        selected = true;
                        ActivateArrow();
                    }
                }
            }
        }
        if(Input.GetMouseButtonUp(0))
        {
            if (selected)
            {
                DeActivateArrow();
                Move();
                selected = false;
            }
        }
    }

    Player getPlayer()
    {
        float maxDistance = 30;
        RaycastHit hit;
        startMousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);
        Ray ray = myMainCamera.ScreenPointToRay(startMousePos);
        if (Physics.Raycast(ray, out hit, maxDistance, LayerMask.GetMask(new string[] {"Player"})))
            return hit.collider.gameObject.GetComponent<Player>();
        else
            return null;
    }

    void ActivateArrow()
    {
        if (GameSettings.Instance.UIArrow != null)
        {
            GameSettings.Instance.UIArrow.gameObject.SetActive(true);
            GameSettings.Instance.UIArrow.SetArrowPosition(Input.mousePosition);
        }
    }

    void DeActivateArrow()
    {
        if (GameSettings.Instance.UIArrow != null)
        {
            GameSettings.Instance.UIArrow.gameObject.SetActive(false);
        }
    }

    void Move()
    {
        Vector2 moveDirectionAndMagintude = MoveMouse();
        myRigidBody.AddForce(new Vector3(moveDirectionAndMagintude.x, 0, moveDirectionAndMagintude.y) * ballSpeed, ForceMode.Impulse);
    }

    Vector2 MoveMouse()
    {
        // caluclate direction
        Vector3 mouseDirection = new Vector3(startMousePos.x, 0, startMousePos.y) - new Vector3(Input.mousePosition.x, 0, Input.mousePosition.y);
        Vector3 camFwd = myMainCamera.gameObject.transform.forward;
        float camAngle = MathE.ConvertVector2ToEulerAngle(new Vector2(camFwd.x, camFwd.z));
        float mouseDirAngle = MathE.ConvertVector2ToEulerAngle(new Vector2(mouseDirection.x, mouseDirection.z));

        // calculate relative magnitude
        Vector3 relativeStartPos = myMainCamera.ScreenToViewportPoint(new Vector3(startMousePos.x, startMousePos.y, 0));
        Vector3 relativeCurrentPos = myMainCamera.ScreenToViewportPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));

        float xMulti = myMainCamera.aspect;
        relativeStartPos = new Vector3(relativeStartPos.x * xMulti, relativeStartPos.y, relativeStartPos.z);
        relativeCurrentPos = new Vector3(relativeCurrentPos.x * xMulti, relativeCurrentPos.y, relativeCurrentPos.z);

        float arrowDist = Vector3.Distance(relativeStartPos, relativeCurrentPos);
        float magnitude = 1;
        if(GameSettings.Instance.UIArrow != null)
            magnitude = Mathf.Clamp(arrowDist, 0, GameSettings.Instance.UIArrow.MaxLineLength);

        return MathE.ConvertEulerAngleToVector2(camAngle + mouseDirAngle, false).normalized * magnitude;
    }
    #endregion
}
