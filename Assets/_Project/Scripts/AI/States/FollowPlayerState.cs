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

            companion.AdjustSpeedForObstacles();

            TargetReading reading = companion.Sensor.SenseTarget(player.position, player);
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
