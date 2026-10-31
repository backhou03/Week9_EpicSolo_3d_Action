using UnityEngine;

public static class NailgunGeometry
{
    // Imported model transforms and the protruding nail are included in the actual tip.
    public static Vector3 MeasureMuzzle(Transform gun, Vector3 fallback, out float forwardSign)
    {
        Bounds contact = default, all = default;
        bool hasContact = false, hasAll = false;
        foreach (var filter in gun.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            bool isContact = false;
            for (Transform part = filter.transform; part != null && part != gun; part = part.parent)
                if (part.name.Contains("_Contact")) isContact = true;
            Bounds b = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(x,y,z));
                        Vector3 local = gun.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (!hasAll) { all = new Bounds(local, Vector3.zero); hasAll = true; }
                        else all.Encapsulate(local);
                        if (isContact)
                        {
                            if (!hasContact) { contact = new Bounds(local, Vector3.zero); hasContact = true; }
                            else contact.Encapsulate(local);
                        }
                    }
        }
        forwardSign = Mathf.Sign(hasContact ? contact.center.z : fallback.z);
        if (forwardSign == 0f) forwardSign = 1f;
        if (!hasAll) return fallback;
        Vector3 muzzle = hasContact ? contact.center : fallback;
        muzzle.z = forwardSign > 0f ? all.max.z : all.min.z;
        return muzzle;
    }

    // Trace the capsule in body-local space, including the visible squash/scale/rotation.
    public static bool CapsuleSurface(Bounds bounds, Vector3 origin, Vector3 direction, out Vector3 point)
    {
        direction.Normalize();
        float radius = Mathf.Min(bounds.extents.x,bounds.extents.z);
        float segment = Mathf.Max(0f,bounds.extents.y-radius);
        float travel = 0f;
        for (int i = 0; i < 96 && travel < 20f; i++)
        {
            point = origin + direction*travel;
            Vector3 q = point-bounds.center;
            q.y -= Mathf.Clamp(q.y,-segment,segment);
            float distance = q.magnitude-radius;
            if (distance <= 0.001f) return true;
            travel += Mathf.Max(0.0005f,distance);
        }
        point = origin;
        return false;
    }
}
