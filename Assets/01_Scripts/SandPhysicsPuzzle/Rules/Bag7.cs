using Random = Unity.Mathematics.Random;

namespace SandPhysicsPuzzle.Rules
{
    // Seven-bag randomizer: each shape appears once per group of seven. Pure chance would deal
    // four S pieces in a row; this bounds the worst drought to twelve pieces.
    public struct Bag7
    {
        private Random _random;
        private int _index;

        // Unrolled rather than an array, so the struct allocates nothing.
        private int _s0, _s1, _s2, _s3, _s4, _s5, _s6;

        public Bag7(uint seed)
        {
            // Random rejects seed 0.
            _random = new Random(seed == 0 ? 1u : seed);
            _index = Tetromino.Count;
            _s0 = _s1 = _s2 = _s3 = _s4 = _s5 = _s6 = 0;
        }

        public int Next()
        {
            if (_index >= Tetromino.Count) Refill();

            var shape = Get(_index);
            _index++;
            return shape;
        }

        private void Refill()
        {
            for (var i = 0; i < Tetromino.Count; i++) Set(i, i);

            // Fisher-Yates
            for (var i = Tetromino.Count - 1; i > 0; i--)
            {
                var j = _random.NextInt(0, i + 1);
                var a = Get(i);
                Set(i, Get(j));
                Set(j, a);
            }

            _index = 0;
        }

        private int Get(int i) => i switch
        {
            0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3, 4 => _s4, 5 => _s5, _ => _s6,
        };

        private void Set(int i, int value)
        {
            switch (i)
            {
                case 0: _s0 = value; break;
                case 1: _s1 = value; break;
                case 2: _s2 = value; break;
                case 3: _s3 = value; break;
                case 4: _s4 = value; break;
                case 5: _s5 = value; break;
                default: _s6 = value; break;
            }
        }
    }
}
