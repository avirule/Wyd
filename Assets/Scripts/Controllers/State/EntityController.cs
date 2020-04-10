#region

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Wyd.Game.Entities;
using Wyd.System.Extensions;

#endregion

namespace Wyd.Controllers.State
{
    public class EntityController : SingletonController<EntityController>
    {
        private Dictionary<Type, List<IEntity>> _EntityRegister;
        private Dictionary<string, List<Action<IEntity>>> _EntityTagWatchers;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _EntityRegister = new Dictionary<Type, List<IEntity>>();
            _EntityTagWatchers = new Dictionary<string, List<Action<IEntity>>>();
        }

        public void RegisterEntity(Type identifyingType, IEntity entity)
        {
            if (!_EntityRegister.ContainsKey(identifyingType))
            {
                _EntityRegister.Add(identifyingType, new List<IEntity>());
            }

            _EntityRegister[identifyingType].Add(entity);

            Log.Information($"Registered entity {identifyingType} (tags: {string.Join(", ", entity.Tags)}).");

            // check for and execute for watched tags
            foreach (string watchedEntityTag in GetMatchedWatchedTags(entity.Tags))
            {
                foreach (Action<IEntity> watchedEntityTagAction in _EntityTagWatchers[watchedEntityTag])
                {
                    watchedEntityTagAction.Invoke(entity);

                    Log.Information($"WatchForTag `{watchedEntityTag}` invoked ({watchedEntityTagAction}).");
                }
            }
        }

        private IEnumerable<string> GetMatchedWatchedTags(IEnumerable<string> entityTags)
        {
            return entityTags.Where(entityTag => _EntityTagWatchers.ContainsKey(entityTag));
        }

        public void RegisterWatchForTag(Action<IEntity> entityRegisteredWithTag, string entityTag)
        {
            if (!_EntityTagWatchers.ContainsKey(entityTag))
            {
                _EntityTagWatchers.Add(entityTag, new List<Action<IEntity>>());
            }

            _EntityTagWatchers[entityTag].Add(entityRegisteredWithTag);
        }

        public void RegisterWatchForTags(Action<IEntity> entityRegisteredWithTagMethod, params string[] entityTags)
        {
            foreach (string entityTag in entityTags)
            {
                RegisterWatchForTag(entityRegisteredWithTagMethod, entityTag);
            }
        }

        public bool TryGetEntityByType(Type entityType, out IEntity entity, params string[] tags)
        {
            if (!_EntityRegister.TryGetValue(entityType, out List<IEntity> entities))
            {
                entity = default;
                return false;
            }

            entity = tags.Length == 0 ? entities.First() : entities.FirstOrDefault(ent => ent.Tags.ContainsAll(tags));
            return entity == default;
        }

        public bool TryGetEntitiesByType(Type entityType, out IList<IEntity> matchedEntities, params string[] tags)
        {
            if (!_EntityRegister.TryGetValue(entityType, out List<IEntity> entities))
            {
                matchedEntities = default;
                return false;
            }

            matchedEntities = entities.Where(entity => entity.Tags.ContainsAll(tags)).ToList();
            return matchedEntities.Count > 0;
        }
    }
}
