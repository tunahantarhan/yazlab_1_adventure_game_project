using UnityEngine;

namespace AdventureGame.AI
{
    // Ajanın göz hizasından Raycast atarak engelleri ve hedefleri algılar.
    // İleri yönde üç ışın (0, -30, +30 derecelerde) engel taraması yapar; hedef için mesafe, açı ve görüş hattı ölçülür.
    public class RaycastCompanionSensor : ICompanionSensor
    {
        private static readonly float[] WhiskerAngles = { 0f, -30f, 30f };

        private readonly Transform owner;
        private readonly float range;
        private readonly int layerMask;
        private readonly float eyeHeight;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public RaycastCompanionSensor(Transform owner, float range, LayerMask obstacleLayer, float eyeHeight = 0.5f)
        {
            this.owner = owner;
            this.range = range;
            this.eyeHeight = eyeHeight;
            // Katman seçilmemişse tüm varsayılan katmanlar taranır
            layerMask = obstacleLayer.value != 0 ? obstacleLayer.value : Physics.DefaultRaycastLayers;
        }

        private Vector3 EyePosition => owner.position + Vector3.up * eyeHeight;

        public ObstacleReading SenseAhead()
        {
            var reading = new ObstacleReading { Distance = range };
            Vector3 origin = EyePosition;

            foreach (float angle in WhiskerAngles)
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * owner.forward;

                if (TryCast(origin, direction, range, null, out RaycastHit hit))
                {
                    Debug.DrawRay(origin, direction * hit.distance, Color.red);
                    if (hit.distance < reading.Distance)
                    {
                        reading.IsBlocked = true;
                        reading.Distance = hit.distance;
                        reading.Angle = angle;
                    }
                }
                else
                {
                    Debug.DrawRay(origin, direction * range, Color.green);
                }
            }

            return reading;
        }

        public TargetReading SenseTarget(Vector3 targetPoint, Transform targetRoot)
        {
            Vector3 flatToTarget = targetPoint - owner.position;
            flatToTarget.y = 0f;

            var reading = new TargetReading
            {
                Distance = flatToTarget.magnitude,
                Angle = Vector3.SignedAngle(owner.forward, flatToTarget, Vector3.up)
            };

            // Görüş hattı göz hizasında, yatay olarak kontrol edilir
            Vector3 origin = EyePosition;
            Vector3 eyeTarget = new Vector3(targetPoint.x, origin.y, targetPoint.z);
            Vector3 toEyeTarget = eyeTarget - origin;
            float distance = toEyeTarget.magnitude;

            if (distance < 0.01f)
            {
                reading.HasLineOfSight = true;
                return reading;
            }

            bool isBlocked = TryCast(origin, toEyeTarget / distance, distance, targetRoot, out RaycastHit hit);
            reading.HasLineOfSight = !isBlocked;
            Debug.DrawLine(origin, isBlocked ? hit.point : eyeTarget, isBlocked ? Color.yellow : Color.cyan);

            return reading;
        }

        // Ajanın kendisine ve hedefin kendisine çarpan ışınlar yok sayılır, en yakın gerçek engel döndürülür
        private bool TryCast(Vector3 origin, Vector3 direction, float maxDistance, Transform ignoreRoot, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, maxDistance, layerMask, QueryTriggerInteraction.Ignore);

            nearest = default;
            bool found = false;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = hitBuffer[i].transform;
                if (hitTransform.IsChildOf(owner)) continue;
                if (ignoreRoot != null && hitTransform.IsChildOf(ignoreRoot)) continue;

                if (hitBuffer[i].distance < nearestDistance)
                {
                    nearestDistance = hitBuffer[i].distance;
                    nearest = hitBuffer[i];
                    found = true;
                }
            }

            return found;
        }
    }
}
