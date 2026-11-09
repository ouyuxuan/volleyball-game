using Sandspike.Art;
using Sandspike.Ball;
using UnityEngine;
using UnityEngine.UI;

namespace Sandspike.CameraRig
{
    /// <summary>
    /// In 1st person, shows an arrow at the edge of the screen pointing toward the ball when the ball
    /// is out of view. Hidden in 3rd person and whenever the ball is on screen.
    /// </summary>
    public class BallIndicator : MonoBehaviour
    {
        [SerializeField] BallController ball;
        [SerializeField] PlayerCameraRig rig;
        [SerializeField] Camera viewCamera;
        [SerializeField] RectTransform arrow;        // a child Image of the HUD canvas
        [SerializeField] float edgeMargin = 70f;     // canvas units from the screen edge

        static Sprite triangle;

        void Awake()
        {
            if (arrow == null) return;
            var image = arrow.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = GetTriangleSprite();
                image.color = Palette.Accent;
                image.raycastTarget = false;
            }
        }

        void LateUpdate()
        {
            if (arrow == null || ball == null || viewCamera == null || rig == null) return;

            bool show = false;
            if (rig.IsFirstPerson)
            {
                Vector3 vp = viewCamera.WorldToViewportPoint(ball.Position);
                bool onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                if (!onScreen)
                {
                    show = true;
                    // Direction from the screen centre toward the ball (flipped if the ball is behind us).
                    Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                    if (vp.z < 0f) dir = -dir;
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector2.down;
                    dir.Normalize();

                    Rect canvas = ((RectTransform)arrow.parent).rect;
                    float halfW = canvas.width * 0.5f - edgeMargin;
                    float halfH = canvas.height * 0.5f - edgeMargin;
                    // Scale the direction until it touches the (margin-shrunk) screen rectangle.
                    float k = Mathf.Min(halfW / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), halfH / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
                    arrow.anchoredPosition = dir * k;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;   // the sprite points up
                    arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        }

        // A small white triangle (pointing up), drawn into a texture so we need no image files.
        static Sprite GetTriangleSprite()
        {
            if (triangle != null) return triangle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ArrowTriangle", filterMode = FilterMode.Bilinear };
            var pixels = new UnityEngine.Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float halfWidth = (1f - y / (float)size) * size * 0.5f;   // wide at the bottom, a point at the top
                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - size * 0.5f) <= halfWidth;
                    pixels[y * size + x] = inside ? new UnityEngine.Color32(255, 255, 255, 255) : new UnityEngine.Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            triangle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return triangle;
        }
    }
}
