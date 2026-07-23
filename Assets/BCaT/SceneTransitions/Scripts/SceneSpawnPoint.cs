using UnityEngine;

public sealed class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId;

    public string SpawnId => spawnId;
}
