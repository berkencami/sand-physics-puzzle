namespace SandPhysicsPuzzle.Rules
{
    // Everything a TurnLoop needs that is not the simulation. Built from the LevelConfig asset.
    public struct TurnLoopSettings
    {
        public uint Seed;

        /// <summary>Sand colours in play. The single biggest difficulty knob.</summary>
        public int ColorCount;

        /// <summary>Grains per tetromino block.</summary>
        public int BlockSize;

        /// <summary>Row that ends the run once settled sand rests on it.</summary>
        public int DangerLineY;

        /// <summary>Ticks a flagged span stays frozen and flashing before it is deleted.</summary>
        public int FlashTicks;

        /// <summary>Chance the next piece repeats the previous colour.</summary>
        public float StickChance;

        /// <summary>How strongly the deal follows the colours already on the board.</summary>
        public float BoardBias;
    }
}
