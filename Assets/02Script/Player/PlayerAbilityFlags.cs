[System.Flags]
public enum PlayerAbilityFlags
{
    None       = 0,
    Jump       = 1 << 0,
    DoubleJump = 1 << 1,
    Dash       = 1 << 2,
    Slam       = 1 << 3,
    All        = Jump | DoubleJump | Dash | Slam,
}
