using UnityEngine;
using VehiclePhysics;

namespace OthDriveSystems
{
    // The game uses two wheel collider classes with identical members; this wraps whichever one a wheel has.
    internal sealed class WheelAdapter
    {
        readonly VPFunselektor3DWheelCollider std;
        readonly VPFunselektor3DRaycastWheelCollider ray;
        public readonly string Kind;

        WheelAdapter(VPFunselektor3DWheelCollider s, VPFunselektor3DRaycastWheelCollider r)
        {
            std = s; ray = r;
            Kind = s != null ? "standard" : "raycast";
        }

        public static WheelAdapter From(WheelColliderBehaviour b)
        {
            if (b == null) return null;
            var s = b.TryCast<VPFunselektor3DWheelCollider>();
            if (s != null) return new WheelAdapter(s, null);
            var r = b.TryCast<VPFunselektor3DRaycastWheelCollider>();
            return r != null ? new WheelAdapter(null, r) : null;
        }

        public bool Alive => std != null ? !std.WasCollected && std != null : ray != null && !ray.WasCollected;
        public float Radius => std != null ? std.radius : ray.radius;
        public float Travel
        {
            get => std != null ? std.suspensionTravel : ray.suspensionTravel;
            set { if (std != null) std.suspensionTravel = value; else ray.suspensionTravel = value; }
        }
        public Transform Visual => std != null ? std.wheelTransform : ray.wheelTransform;
        public void SetRadius(float r) { if (std != null) std.SetWheelRadius(r); else ray.SetWheelRadius(r); }
        public void Refresh() { if (std != null) std.RefreshPhysics(); else ray.RefreshPhysics(); }
    }
}
