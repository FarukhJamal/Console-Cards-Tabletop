using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Project-wide physics feel for the tabletop. Call Apply() once, before any physical objects are
    /// initialized (e.g. at the top of TabletopPrototypeComposition.Awake). Also mirror the numbers in
    /// Project Settings > Physics / Time so the editor and builds agree.
    /// </summary>
    public static class TabletopPhysicsSettings
    {
        // Game-scale gravity. Objects here are ~1 unit wide, so real-world -9.81 looks floaty.
        // Tune between -30 and -70. Higher = snappier drops and shorter dice flights.
        public static float Gravity = -40f;
        public static float FixedTimeStep = 0.01f;           // 100 Hz: fast dice need it
        public static int SolverIterations = 10;
        public static int SolverVelocityIterations = 4;

        private static PhysicsMaterial tableMaterial;
        private static PhysicsMaterial dieMaterial;
        private static PhysicsMaterial pieceMaterial;

        public static void Apply()
        {
            Physics.gravity = new Vector3(0f, Gravity, 0f);
            Time.fixedDeltaTime = FixedTimeStep;
            Physics.defaultSolverIterations = SolverIterations;
            Physics.defaultSolverVelocityIterations = SolverVelocityIterations;
            Physics.defaultContactOffset = 0.005f;
            Physics.bounceThreshold = 1f;
            Physics.defaultMaxDepenetrationVelocity = 10f;
        }

        /// <summary>Assign to every PhysicalTabletopSurface collider (table, boards, mats).</summary>
        public static PhysicsMaterial Table => tableMaterial != null ? tableMaterial : (tableMaterial = Create("TabletopSurface", 0.5f, 0.6f, 0.20f));

        /// <summary>Bouncy, moderate friction: dice should hop and tumble.</summary>
        public static PhysicsMaterial Die => dieMaterial != null ? dieMaterial : (dieMaterial = Create("TabletopDie", 0.4f, 0.5f, 0.38f));

        /// <summary>Cards, pawns, tokens: grippy, almost no bounce.</summary>
        public static PhysicsMaterial Piece => pieceMaterial != null ? pieceMaterial : (pieceMaterial = Create("TabletopPiece", 0.6f, 0.7f, 0.05f));

        public static PhysicsMaterial MaterialFor(bool isDie) => isDie ? Die : Piece;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterials()
        {
            tableMaterial = null;
            dieMaterial = null;
            pieceMaterial = null;
        }

        private static PhysicsMaterial Create(string name, float dynamicFriction, float staticFriction, float bounce)
        {
            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounce,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum, // so a bouncy die bounces off a dead table
            };
            return material;
        }
    }
}
