using UnityEngine;

namespace AdventureGame.AI
{
    // Ajanın önündeki en yakın engelin bilgisi
    public struct ObstacleReading
    {
        public bool IsBlocked;
        public float Distance;
        public float Angle; // engeli gören ışının ajanın ileri yönüne göre açısı
    }

    // Ajanın bir hedefe göre konumu
    public struct TargetReading
    {
        public float Distance;
        public float Angle; // hedefin ajanın ileri yönüne göre işaretli açısı (-180, 180)
        public bool HasLineOfSight;
    }

    public interface ICompanionSensor
    {
        ObstacleReading SenseAhead();
        TargetReading SenseTarget(Vector3 targetPoint, Transform targetRoot);
    }
}
