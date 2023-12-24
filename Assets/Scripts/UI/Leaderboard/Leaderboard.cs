using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;
    [SerializeField] private Transform teamLeaderboardEntityHolder;
    [SerializeField] private GameObject teamLeaderboardBackground;
    [SerializeField] private LeaderboardEntityDisplay leaderboardEntityPrefab;
    [SerializeField] private int entitiesToDisplay = 8;
    [SerializeField] private Color ownerColor;
    [SerializeField] private string[] teamNames;
    [SerializeField] private TeamColorLookup teamColorLookup;

    private NetworkList<LeaderboardEntityState> leaderboardEntities;
    private List<LeaderboardEntityDisplay> entityDisplays = new List<LeaderboardEntityDisplay>();
    private List<LeaderboardEntityDisplay> teamEntityDisplays = new List<LeaderboardEntityDisplay>();
    private LeaderboardEntityDisplay myDisplay;
    private LeaderboardEntityDisplay teamDisplay;

    private void Awake()
    {
        leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            if (ClientSingleton.Instance.GameManager.UserData.userGamePreferences.gameQueue == GameQueue.Team)
            {
                teamLeaderboardBackground.SetActive(true);

                for (int i=0;i<teamNames.Length;i++)
                {
                    LeaderboardEntityDisplay teamLeaderboardEntity = Instantiate(leaderboardEntityPrefab, teamLeaderboardEntityHolder);

                    teamLeaderboardEntity.Initialize(i, teamNames[i], 0);

                    Color teamColor = teamColorLookup.GetTeamColor(i);
                    teamLeaderboardEntity.SetColor(teamColor);

                    teamEntityDisplays.Add(teamLeaderboardEntity);
                }
            }

            leaderboardEntities.OnListChanged += HandleLeaderboardEntitiesChanged;

            foreach (LeaderboardEntityState entity in leaderboardEntities)
            {
                HandleLeaderboardEntitiesChanged(new NetworkListEvent<LeaderboardEntityState>
                {
                    Type = NetworkListEvent<LeaderboardEntityState>.EventType.Add,
                    Value = entity
                });
            }
        }

        if (IsServer)
        {
            TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);

            foreach (TankPlayer player in players)
            {
                HandlePlayerSpawned(player);
            }

            TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
            TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged -= HandleLeaderboardEntitiesChanged;
        }

        if (IsServer)
        {
            TankPlayer.OnPlayerSpawned -= HandlePlayerSpawned;
            TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;
        }
    }

    private void HandleLeaderboardEntitiesChanged(NetworkListEvent<LeaderboardEntityState> changeEvent)
    {
        if (!gameObject.scene.isLoaded) return;

        switch (changeEvent.Type)
        {
            case NetworkListEvent<LeaderboardEntityState>.EventType.Add:
                if (!entityDisplays.Any(x => x.ClientId == changeEvent.Value.ClientId))
                {
                    LeaderboardEntityDisplay leaderboardEntity =
                        Instantiate(leaderboardEntityPrefab, leaderboardEntityHolder);
                    leaderboardEntity.Initialize(
                        changeEvent.Value.ClientId,
                        changeEvent.Value.PlayerName,
                        changeEvent.Value.Coins);
                    if (NetworkManager.Singleton.LocalClientId == changeEvent.Value.ClientId)
                    {
                        leaderboardEntity.SetColor(ownerColor);
                    }
                    entityDisplays.Add(leaderboardEntity);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                LeaderboardEntityDisplay displayToRemove =
                    entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (displayToRemove != null)
                {
                    displayToRemove.transform.SetParent(null);
                    entityDisplays.Remove(displayToRemove);
                    Destroy(displayToRemove.gameObject);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Value:
                LeaderboardEntityDisplay displayToUpdate =
                    entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (displayToUpdate != null)
                {
                    displayToUpdate.UpdateCoins(changeEvent.Value.Coins);
                }
                break;
            default:
                break;
        }

        entityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

        for (int i = 0; i < entityDisplays.Count; i++)
        {
            entityDisplays[i].transform.SetSiblingIndex(i);
            entityDisplays[i].UpdateText();
            bool shouldShow = i <= entitiesToDisplay - 1;
            entityDisplays[i].gameObject.SetActive(shouldShow);
        }

        myDisplay = entityDisplays.FirstOrDefault(x => x.ClientId == NetworkManager.Singleton.LocalClientId);
        if (myDisplay != null)
        {
            if (myDisplay.transform.GetSiblingIndex() >= entitiesToDisplay)
            {
                leaderboardEntityHolder.GetChild(entitiesToDisplay - 1).gameObject.SetActive(false);
                myDisplay.gameObject.SetActive(true);
            }
        }

        if (!teamLeaderboardBackground.activeSelf) return;
        
        teamDisplay = teamEntityDisplays.FirstOrDefault(x => x.TeamIndex == changeEvent.Value.TeamIndex);

        if (teamDisplay != null)
        {
            if (changeEvent.Type == NetworkListEvent<LeaderboardEntityState>.EventType.Remove)
            {
                teamDisplay.UpdateCoins(teamDisplay.Coins - changeEvent.Value.Coins);
            }
            else
            {
                teamDisplay.UpdateCoins(teamDisplay.Coins + (changeEvent.Value.Coins - changeEvent.PreviousValue.Coins));
            }

            teamEntityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

            for (int i=0;i<teamEntityDisplays.Count; i++)
            {
                teamEntityDisplays[i].transform.SetSiblingIndex(i);
                teamEntityDisplays[i].UpdateText();
            }
        }

    }

    private void HandlePlayerSpawned(TankPlayer player)
    {
        leaderboardEntities.Add(new LeaderboardEntityState
        {
            ClientId = player.OwnerClientId,
            PlayerName = player.PlayerName.Value,
            TeamIndex = player.TeamIndex.Value,
            Coins = 0
        });

        player.Wallet.TotalCoins.OnValueChanged += (oldCoins, newCoins) => HandleCoinsChanged(player.OwnerClientId, newCoins);
    }

    private void HandlePlayerDespawned(TankPlayer player)
    {
        if (leaderboardEntities != null)
        {
            foreach (LeaderboardEntityState entity in leaderboardEntities)
            {
                if (entity.ClientId == player.OwnerClientId)
                {
                    leaderboardEntities.Remove(entity);
                    break;
                }
            }
        }

        player.Wallet.TotalCoins.OnValueChanged -= (oldCoins, newCoins) => HandleCoinsChanged(player.OwnerClientId, newCoins);
    }

    private void HandleCoinsChanged(ulong clientId, int newCoins)
    {
        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId == clientId)
            {
                leaderboardEntities[i] = new LeaderboardEntityState
                {
                    ClientId = leaderboardEntities[i].ClientId,
                    PlayerName = leaderboardEntities[i].PlayerName,
                    TeamIndex = leaderboardEntities[i].TeamIndex,
                    Coins = newCoins
                };
                break;
            }
        }
    }
}
