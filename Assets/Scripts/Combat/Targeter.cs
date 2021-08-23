using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Targeter : NetworkBehaviour
{
    private Targetable target;

    public Targetable getTarget()
    {
        return target;
    }

    public override void OnStartServer()
    {
        GameOverHandler.ServerOnGameOver -= ServerHandleGameOver;
    }

    public override void OnStopServer()
    {
        GameOverHandler.ServerOnGameOver -= ServerHandleGameOver;
    }

    #region Server

    [Command]
    public void CmdSetTarget(GameObject targetGameObject)
    {
        if (!targetGameObject.TryGetComponent<Targetable>(out Targetable target)) { return; }

        this.target = target;
    }

    [Server]
    public void ClearTarget()
    {
        target = null;
    }

    [Server]
    private void ServerHandleGameOver()
    {
        ClearTarget();
    }

    #endregion


}
