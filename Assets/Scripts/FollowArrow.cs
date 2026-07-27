using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class FollowArrow : MonoBehaviour
{
    #region Fields
    [SerializeField] float maxLineLength = 0.3f;
    [SerializeField] Color emptyColor = Color.red;
    [SerializeField] Color fullColor = Color.green;
    RectTransform myRectTransform;
    Vector3 startPos;
    Vector3 direction;
    Camera myMainCamera;
    Image myArrowImage;
    float arrowDist;
    #endregion

    #region Properties
    public float MaxLineLength
    {
        get { return maxLineLength; }
    }
    #endregion

    #region Methods
    private void Awake()
    {
        myRectTransform = GetComponent<RectTransform>();
        myArrowImage = GetComponentInChildren<Image>();
        myMainCamera = Camera.main;
    }
    private void Update()
    {
        MoveArrow();
        ChangeArrowVisual();
    }
    public void SetArrowPosition(Vector2 newPosition)
    {
        startPos = new Vector3(newPosition.x, newPosition.y, 0);
        myRectTransform.position = startPos;
    }
    void MoveArrow()
    {
        Vector3 currentPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0);

        // Limit line magnitude
        Vector3 relativeStartPos = myMainCamera.ScreenToViewportPoint(startPos);
        Vector3 relativeCurrentPos = myMainCamera.ScreenToViewportPoint(currentPos);

        float xMulti = myMainCamera.aspect;
        relativeStartPos = new Vector3(relativeStartPos.x * xMulti, relativeStartPos.y, relativeStartPos.z);
        relativeCurrentPos = new Vector3(relativeCurrentPos.x * xMulti, relativeCurrentPos.y, relativeCurrentPos.z);

        direction = startPos - currentPos;
        float dirMagnitude = direction.magnitude;
        arrowDist = Vector3.Distance(relativeStartPos, relativeCurrentPos);

        if (maxLineLength > 0)
        {
            if (arrowDist > maxLineLength)
            {
                float diff = maxLineLength / arrowDist;
                dirMagnitude *= diff;
            }
        }

        // Set Line Magnitude and angle
        myRectTransform.sizeDelta = new Vector2(myRectTransform.sizeDelta.x, dirMagnitude);
        myRectTransform.eulerAngles = new Vector3(myRectTransform.eulerAngles.x, myRectTransform.eulerAngles.y,
            MathE.ReturnMirrorEulerAngle(MathE.ConvertVector2ToEulerAngle(direction)));
    }
    void ChangeArrowVisual()
    {

        if (myArrowImage != null)
        {
            if(maxLineLength > 0)
                myArrowImage.color = Color.Lerp(emptyColor, fullColor, arrowDist / maxLineLength);
        }
    }
    #endregion
}
