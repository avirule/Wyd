#region

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Wyd.Entities;
using Wyd.Extensions;

#endregion

namespace Wyd.Controllers.State
{
    public delegate void EntityWatchForCallback(IEntity entity);

    public class EntityController : SingletonController<EntityController>
    {
        private Dictionary<Type, List<IEntity>> _EntityRegister;
        private Dictionary<string, List<EntityWatchForCallback>> _EntityTagWatchers;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _EntityRegister = new Dictionary<Type, List<IEntity>>();
            _EntityTagWatchers = new Dictionary<string, List<EntityWatchForCallback>>();
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
                foreach (EntityWatchForCallback watchedEntityTagAction in _EntityTagWatchers[watchedEntityTag])
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

        public void RegisterWatchForTag(EntityWatchForCallback entityWatchForCallback, string entityTag)
        {
            if (!_EntityTagWatchers.ContainsKey(entityTag))
            {
                _EntityTagWatchers.Add(entityTag, new List<EntityWatchForCallback>());
            }

            _EntityTagWatchers[entityTag].Add(entityWatchForCallback);
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
