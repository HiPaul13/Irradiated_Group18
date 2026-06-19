public static class SceneTransitionState
{
    public static string NextSpawnPointId { get; set; }
    public static bool IsTransitioning { get; set; }

    public static void Clear()
    {
        NextSpawnPointId = null;
        IsTransitioning = false;
    }
}
