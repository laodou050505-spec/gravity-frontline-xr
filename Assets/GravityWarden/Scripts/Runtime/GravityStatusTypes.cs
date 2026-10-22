namespace GravityRepulsion
{
    /// <summary>
    /// Icon / state displayed by the visor and generated HUD notice area.
    /// Replaces the previous message.Contains("中文") icon-selection logic.
    /// </summary>
    public enum HudStatusEvent
    {
        Ready   = 0,   // idle or neutral feedback
        Holding = 1,   // hand is gripping a target
        Locked  = 2,   // candidate target is locked
        Danger  = 3,   // shield damage received
        Impact  = 4,   // target destroyed / collision
    }

    /// <summary>
    /// Semantic category for how a GravityTarget was destroyed.
    /// Used by RecordEnemyScore to award bonus points without string matching.
    /// </summary>
    public enum DestroyReason
    {
        Default                = 0,  // generic / no bonus
        ReflectAttack          = 1,  // enemy projectile reflected back
        RepulseAttack          = 2,  // repulse ability kill
        BipolarCollision       = 3,  // two-handed bipolar collision
        ChainReaction          = 4,  // environment / chain reaction
        SingularityAnnihilation = 5, // singularity (ultimate) ability
        PrecisionDefense       = 6,  // blocked by held object
        HealthPack             = 7,  // health pack consumed
        SmokeTest              = 8,  // automated QA — never scores
    }
}
