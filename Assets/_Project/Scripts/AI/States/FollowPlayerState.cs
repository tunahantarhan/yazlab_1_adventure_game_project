namespace AdventureGame.AI.States
{
    // Oyuncu uzaktaysa veya görüş hattında değilse (duvar arkası) oyuncuya doğru yol bulur, yakın ve görünürse bekler
    public class FollowPlayerState : CompanionStateBase
    {
        public FollowPlayerState(CompanionController companion) : base(companion) { }

        public override CompanionStateType Type => CompanionStateType.FollowPlayer;

        public override void UpdateState()
        {
            var player = companion.PlayerTarget;
            if (player == null) return;

            TargetReading reading = companion.Sensor.SenseTarget(player.position, player);

            // Oyuncu koşup uzaklaşırsa companion da koşarak yetişir, yaklaşınca tekrar yürür
            companion.SetRunning(reading.Distance > companion.RunDistance);
            companion.AdjustSpeedForObstacles();

            if (reading.Distance > companion.FollowDistance || !reading.HasLineOfSight)
            {
                companion.MoveTo(player.position);
            }
            else
            {
                companion.StopMoving();
            }
        }
    }
}
