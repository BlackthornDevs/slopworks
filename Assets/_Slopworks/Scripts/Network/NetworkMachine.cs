using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkMachine : NetworkBehaviour
{
    private Machine _machine;
    private MachineDefinitionSO _definition;

    private readonly SyncVar<byte> _status = new();
    private readonly SyncVar<string> _activeRecipeId = new();
    private readonly SyncVar<float> _craftProgress = new();

    public Machine Machine => _machine;
    public MachineStatus Status => (MachineStatus)_status.Value;
    public string ActiveRecipeId => _activeRecipeId.Value;
    public float CraftProgress => _craftProgress.Value;

    public void ServerInit(MachineDefinitionSO definition, Machine machine)
    {
        _definition = definition;
        _machine = machine;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_machine == null && _definition != null)
            _machine = new Machine(_definition);
    }

    public void ServerSyncState()
    {
        if (_machine == null) return;
        SyncState();
    }

    private void SyncState()
    {
        _status.Value = (byte)_machine.Status;
        _activeRecipeId.Value = _machine.ActiveRecipeId;
        _craftProgress.Value = _machine.CraftProgress;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSetRecipe(string recipeId)
    {
        if (!IsServerInitialized || _machine == null) return;
        _machine.SetRecipe(recipeId);
        SyncState();
        Debug.Log($"machine: recipe set to {recipeId}");
    }
}
