using Sandspike.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandspike.Aim
{
    /// <summary>
    /// 3rd person aiming. With a mouse, the cursor is a free pointer: we shoot a ray from the camera
    /// through it onto the ground plane (y = 0). With a gamepad, the right stick pushes the aim point
    /// around the sand from wherever it currently is.
    /// </summary>
    public class CursorAim : MonoBehaviour, IAimSource
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Vector3 startPoint = new Vector3(0f, 0f, 4f);

        [Header("Gamepad stick aim")]
        [SerializeField] float stickAimSpeed = 10f;          // metres per second at full stick
        [SerializeField] float stickDeadZone = 0.2f;
        [SerializeField] Vector2 areaHalfSize = new Vector2(8f, 12f);   // x and z limits around the court centre

        Vector3 aimPoint;

        public Vector3 AimPoint => aimPoint;

        void Awake() { aimPoint = startPoint; }

        /// <summary>Called by the camera rig each frame while 3rd person is active. stick = right stick value.</summary>
        public void Tick(Vector2 rawStick, float dt)
        {
            if (InputDevices.UsingGamepad)
            {
                Vector2 s = Sandspike.Players.HumanBrain.ApplyDeadZone(rawStick, stickDeadZone);
                // stick up = further from the camera (+z), stick right = +x
                aimPoint += new Vector3(s.x, 0f, s.y) * (stickAimSpeed * dt);
                aimPoint.x = Mathf.Clamp(aimPoint.x, -areaHalfSize.x, areaHalfSize.x);
                aimPoint.z = Mathf.Clamp(aimPoint.z, -areaHalfSize.y, areaHalfSize.y);
                aimPoint.y = 0f;
            }
            else if (Mouse.current != null && viewCamera != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                if (RayToGround(viewCamera.ScreenPointToRay(screen), out Vector3 p))
                {
                    p.x = Mathf.Clamp(p.x, -areaHalfSize.x, areaHalfSize.x);
                    p.z = Mathf.Clamp(p.z, -areaHalfSize.y, areaHalfSize.y);
                    aimPoint = p;
                }
            }
        }

        /// <summary>Where does this ray hit the plane y = 0? False if it points up or level.</summary>
        public static bool RayToGround(Ray ray, out Vector3 point)
        {
            point = default;
            if (ray.direction.y > -1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            point = ray.origin + ray.direction * t;
            point.y = 0f;
            return true;
        }

        /// <summary>Lets other code (3rd person entry, tests) put the aim point somewhere.</summary>
        public void SetAimPoint(Vector3 p) { p.y = 0f; aimPoint = p; }
    }
}
