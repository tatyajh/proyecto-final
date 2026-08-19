using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Components")]
    [SerializeField] private RectTransform containerBackground;
    [SerializeField] private RectTransform joystickHandle;

    private Vector2 inputVector = Vector2.zero;

    public Vector2 InputVector => inputVector;

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerBackground, 
            eventData.position, 
            eventData.pressEventCamera, 
            out position))
        {
            position.x = (position.x / containerBackground.sizeDelta.x);
            position.y = (position.y / containerBackground.sizeDelta.y);

            inputVector = new Vector2(position.x * 2 - 1, position.y * 2 - 1);
            inputVector = (inputVector.magnitude > 1.0f) ? inputVector.normalized : inputVector;

            // Mover la perilla del Joystick
            joystickHandle.anchoredPosition = new Vector2(
                inputVector.x * (containerBackground.sizeDelta.x / 3),
                inputVector.y * (containerBackground.sizeDelta.y / 3)
            );
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;
    }
}