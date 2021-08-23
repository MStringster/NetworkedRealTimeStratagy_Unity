using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    [SyncVar (hook = nameof(HandleHealthUpdate))]
    private int currentHealth;

    public event Action ServerOnDie;

    public event Action<int, int> ClientOnHealthUpdate;

    #region Server 

    public override void OnStartServer()
    {
        currentHealth = maxHealth;
        UnitBase.ServerOnPlayerDie += ServerHandlePlayerDie;
    }

    public override void OnStopServer()
    {
        UnitBase.ServerOnPlayerDie -= ServerHandlePlayerDie;
    }

    [Server]
    public void DealDamage(int damageAmount)
    {
        if(currentHealth == 0) { return; }

        currentHealth = Mathf.Max(currentHealth - damageAmount, 0);

        if(currentHealth != 0) { return; }

        ServerOnDie?.Invoke();

        Debug.Log("Dieded"); 
    }

    [Server]
    private void ServerHandlePlayerDie(int connectionId)
    {
        if(connectionToClient.connectionId != connectionId) { return; }

        DealDamage(currentHealth);
    }

    #endregion

    #region Client

    private void HandleHealthUpdate(int oldHealth, int newHealth)
    {
        ClientOnHealthUpdate?.Invoke(newHealth, maxHealth);
    }

    #endregion
}
