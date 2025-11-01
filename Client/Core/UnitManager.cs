using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    public class UnitManager : IDisposable
    {
        private readonly List<GameObject> _managedRemotePlayers = new List<GameObject>();
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        public int RemotePlayerCount => _managedRemotePlayers.Count;
        public IReadOnlyList<GameObject> ManagedRemotePlayers => _managedRemotePlayers.AsReadOnly();

        public UnitManager()
        {
            if (GameContext.IsInitialized)
            {
                SubscribeToEvents();
            }
        }

        private void SubscribeToEvents()
        {
            _eventSubscriber.Subscribe<CreateRemoteCharacterRequestEvent>(OnCreateRemoteCharacterRequested);
            _eventSubscriber.EnsureInitializedAndSubscribe();
        }

        private void OnCreateRemoteCharacterRequested(CreateRemoteCharacterRequestEvent evt)
        {
            var character = CreateRemotePlayer(evt.PlayerId, Vector3.zero);
            PublishCharacterCreated(evt.PlayerId, character);
        }

        private void PublishCharacterCreated(string playerId, GameObject? character)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteCharacterCreatedEvent(playerId, character));
            }
        }

        public GameObject? CreateRemotePlayer(string playerId, Vector3 position)
        {
            var characterItem = CharacterCreationUtils.CreateCharacterItem();
            if (characterItem == null) return null;

            var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
            if (modelPrefab == null) return null;

            var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                characterItem, modelPrefab, position, Quaternion.identity
            );
            if (newCharacter == null) return null;

            CharacterCreationUtils.ConfigureCharacter(newCharacter, $"RemotePlayer_{playerId}", position, team: 0);
            CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, "测试名字", showName: true);
            CharacterCreationUtils.RequestHealthBar(newCharacter);

            Component? characterComponent = newCharacter as Component;
            if (characterComponent != null)
            {
                _managedRemotePlayers.Add(characterComponent.gameObject);
            }

            return characterComponent?.gameObject;
        }

        public bool DestroyRemotePlayer(GameObject player)
        {
            if (player == null) return false;

            if (_managedRemotePlayers.Remove(player))
            {
                UnityEngine.Object.Destroy(player);
                return true;
            }
            return false;
        }

        public void DestroyAllRemotePlayers()
        {
            foreach (var player in _managedRemotePlayers)
            {
                if (player != null)
                {
                    UnityEngine.Object.Destroy(player);
                }
            }
            _managedRemotePlayers.Clear();
        }

        public void EnsureSubscribed()
        {
            _eventSubscriber.EnsureInitializedAndSubscribe();
        }

        public void Dispose()
        {
            _eventSubscriber?.Dispose();
            DestroyAllRemotePlayers();
        }
    }
}


