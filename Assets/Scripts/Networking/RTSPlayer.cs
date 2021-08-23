using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTSPlayer : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform = null;
    [SerializeField] private LayerMask buildingBlockLayer = new LayerMask();
    [SerializeField] private Building[] buildings = new Building[0];
    [SerializeField] private float buildingRangeLimit = 5f;

    [SyncVar(hook = nameof(ClientHandleResourcesUpdate))]
    private int resources = 500;

    [SyncVar(hook = nameof(AuthorityHandleHostStateUpdated))]
    private bool isHost = false;

    [SyncVar(hook = nameof(ClientHandleDisplayNameUpdated))]
    private string displayName;

    

    public event Action<int> ClientOnResourcesUpdated;

    public static event Action ClientOnInfoUpdated;
    public static event Action<bool> AuthorityOnHostStateUpdated;

    private Color teamColor = new Color();
    private List<Unit> myUnits = new List<Unit>();
    private List<Building> myBuildings = new List<Building>();

    public string GetDisplayName()
    {
        return displayName;
    }

    public bool GetIsHost()
    {
        return isHost;
    }

    public Transform GetCameraTransform()
    {
        return cameraTransform;
    }

    public Color GetTeamColor()
    {
        return teamColor;
    }

    public int GetResources()
    {
        return resources;
    }

    public List<Unit> GetMyUnits()
    {
        return myUnits;
    }

    public List<Building> GetBuildings()
    {
        return myBuildings;
    }


    public bool CanPlaceBuilding(BoxCollider buildingCollider, Vector3 location)
    {
        if (Physics.CheckBox(location + buildingCollider.center, buildingCollider.size, Quaternion.identity, buildingBlockLayer))
        {
            return false;
        }
        foreach (Building building in myBuildings)
        {
            if ((location - building.transform.position).sqrMagnitude <= buildingRangeLimit * buildingRangeLimit)
            {
                return true;
            }
        }
        return false;
    }

    #region Server
    public override void OnStartServer()
    {
        Unit.ServerOnUnitSpawned += ServerHandleUnitSpawned;
        Unit.ServerOnUnitDespawned += ServerHandleUnitDespawned;
        Building.ServerOnBuildingSpawned += ServerHandleBuildingSpawned;
        Building.ServerOnBuildingDespawned += ServerHandleBuildingDespawned;

        DontDestroyOnLoad(gameObject);
    }


    public override void OnStopServer()
    {
        Unit.ServerOnUnitSpawned -= ServerHandleUnitSpawned;
        Unit.ServerOnUnitDespawned -= ServerHandleUnitDespawned;
        Building.ServerOnBuildingSpawned -= ServerHandleBuildingSpawned;
        Building.ServerOnBuildingDespawned -= ServerHandleBuildingDespawned;
    }
    [Server]
    public void SetDisplayName(string displayName)
    {
        this.displayName = displayName;
    }

    [Server]
    public void SetTeamColor(Color newTeamColor)
    {
        teamColor = newTeamColor;
    }

    [Server]
    public void SetResources(int newResources)
    {
        resources = newResources;
    }

    [Server]
    public void SetHost(bool state)
    {
        isHost = state;
    }

    [Command]
    public void CmdStartGame()
    {
        if(!isHost) { return; }

        ((RTSNetworkManager)NetworkManager.singleton).StartGame();
    }

    [Command]
    public void CmdTryPlaceBuilding(int buildingId, Vector3 location)
    {
        Building buildingToPlace = null;

        foreach(Building building in buildings)
        {
            if(building.GetId() == buildingId)
            {
                buildingToPlace = building;
                break;
            }
        }

        if(buildingToPlace == null) { return; }

        if(resources < buildingToPlace.GetPrice()) { return; }

        BoxCollider buildingCollider = buildingToPlace.GetComponent<BoxCollider>();

       if(!CanPlaceBuilding(buildingCollider, location)) { return; }

        GameObject buildingInstance = Instantiate(buildingToPlace.gameObject, location, buildingToPlace.transform.rotation);

        NetworkServer.Spawn(buildingInstance, connectionToClient);
        SetResources(resources - buildingToPlace.GetPrice());
    }

    private void ServerHandleUnitSpawned(Unit unit)
    {
        if(unit.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myUnits.Add(unit);

    }

    private void ServerHandleUnitDespawned(Unit unit)
    {
        if (unit.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myUnits.Remove(unit);
    }

    private void ServerHandleBuildingSpawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        myBuildings.Add(building);

    }

    private void ServerHandleBuildingDespawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) { return; }
        if(!myBuildings.Contains(building)) { return; }
        myBuildings.Remove(building);
    }

    #endregion

    #region Client
    private void ClientHandleResourcesUpdate(int oldResources, int NewResources)
    {
        ClientOnResourcesUpdated?.Invoke(NewResources);
    }

    private void ClientHandleDisplayNameUpdated(string oldDisplayName, string newDisplayName)
    {
        ClientOnInfoUpdated?.Invoke();
    }

    public override void OnStartAuthority()
    {
        if(NetworkServer.active) { return; }
        Unit.AuthorityOnUnitSpawned += AuthorityHandleUnitSpawned;
        Unit.ServerOnUnitDespawned += AuthorityHandleUnitDespawned;
        Building.AuthorityOnBuildingSpawned += AuthorityHandleBuildingSpawned;
        Building.AuthorityOnBuildingSpawned += AuthorityHandleBuildingDespawned;
    }

    public override void OnStartClient()
    {
        if(NetworkServer.active) { return; }

        DontDestroyOnLoad(gameObject);

        ((RTSNetworkManager)NetworkManager.singleton).Players.Add(this);
    }
    public override void OnStopClient()
    {
        ClientOnInfoUpdated?.Invoke();

        if (!isClientOnly) { return; }

        ((RTSNetworkManager)NetworkManager.singleton).Players.Remove(this);

        if(!hasAuthority) { return; }

        Unit.AuthorityOnUnitSpawned -= AuthorityHandleUnitSpawned;
        Unit.AuthorityOnUnitDespawned -= AuthorityHandleUnitDespawned;

        Building.AuthorityOnBuildingSpawned -= AuthorityHandleBuildingSpawned;
        Building.AuthorityOnBuildingSpawned -= AuthorityHandleBuildingDespawned;
    }

    private void AuthorityHandleHostStateUpdated(bool oldState, bool newState)
    {
        if(!hasAuthority) { return; }

        AuthorityOnHostStateUpdated?.Invoke(newState);

    }

    private void AuthorityHandleUnitSpawned(Unit unit)
    {
        myUnits.Add(unit);
    }

    private void AuthorityHandleUnitDespawned(Unit unit)
    {
        myUnits.Remove(unit);
    }

    private void AuthorityHandleBuildingSpawned(Building building)
    {
        myBuildings.Add(building);
    }

    private void AuthorityHandleBuildingDespawned(Building building)
    {
        myBuildings.Remove(building);
    }
    #endregion
}
