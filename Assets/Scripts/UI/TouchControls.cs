using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On-screen controls for phones: drag joystick (bottom-left) and a hold-to-run button
// (bottom-right). They write to VirtualInput, which PlayerController reads.
public class TouchControls : MonoBehaviour
{
    public void Build(RectTransform root)
    {
        // Joystick: a large invisible touch area so it's easy to grab.
        var area = UIStyle.Rect("JoystickArea", root);
        UIStyle.Anchor(area, new Vector2(0, 0), new Vector2(40, 30), new Vector2(520, 460));
        area.gameObject.AddComponent<Image>().color = Color.clear;

        var ring = UIStyle.Icon("Ring", area, UIStyle.RingIcon, new Color(1, 1, 1, 0.55f), 280);
        UIStyle.Anchor(ring.rectTransform, new Vector2(0, 0), new Vector2(110, 90), new Vector2(280, 280));
        var knob = UIStyle.Icon("Knob", ring.transform, UIStyle.DotIcon, new Color(1, 1, 1, 0.75f), 120);
        knob.gameObject.AddComponent<Outline>().effectColor = UIStyle.Ink;
        knob.rectTransform.anchoredPosition = Vector2.zero;

        var stick = area.gameObject.AddComponent<Joystick>();
        stick.ring = ring.rectTransform;
        stick.knob = knob.rectTransform;

        // Run button.
        var run = UIStyle.Panel("Run", root, radius: 60, border: 6);
        UIStyle.Anchor(run.rectTransform, new Vector2(1, 0), new Vector2(-90, 90), new Vector2(220, 220));
        run.color = new Color(1, 1, 1, 0.9f);
        var label = UIStyle.Label("Text", run.transform, 46, UIStyle.Paper);
        label.text = "RUN";
        UIStyle.Stretch(label.rectTransform);
        run.gameObject.AddComponent<RunButton>().background = run;
    }

    class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform ring, knob;
        const float Radius = 120f;

        public void OnPointerDown(PointerEventData e)
        {
            // Re-centre the stick where the thumb lands.
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ring.parent, e.position, e.pressEventCamera, out var local);
            ring.localPosition = local;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ring, e.position, e.pressEventCamera, out var local);
            Vector2 v = Vector2.ClampMagnitude(local, Radius);
            knob.anchoredPosition = v;
            VirtualInput.Move = v / Radius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            knob.anchoredPosition = Vector2.zero;
            VirtualInput.Move = Vector2.zero;
        }

        void OnDisable() => VirtualInput.Move = Vector2.zero;
    }

    class RunButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Image background;

        public void OnPointerDown(PointerEventData e)
        {
            VirtualInput.Sprint = true;
            transform.localScale = Vector3.one * 0.92f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            VirtualInput.Sprint = false;
            transform.localScale = Vector3.one;
        }

        void OnDisable() => VirtualInput.Sprint = false;
    }
}
