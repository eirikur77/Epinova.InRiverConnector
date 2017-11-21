﻿using System;
using System.Collections.Generic;
using System.Linq;
using Epinova.InRiverConnector.EpiserverAdapter.Helpers;
using inRiver.Remoting;
using inRiver.Remoting.Objects;

namespace Epinova.InRiverConnector.EpiserverAdapter
{
    public class EntityService : IEntityService
    {
        private readonly IConfiguration _config;
        private readonly EpiMappingHelper _mappingHelper;

        /// <summary>
        /// Very simple local cache of all entities. FlushCache() empties the list. GetEntity should retrieve from this list if possible, to
        /// avoid hitting the InRiver server API all the time. Saving loads of time on large publishes.
        /// </summary>
        private List<Entity> _cachedEntities;

        private Dictionary<string, List<StructureEntity>> _cachedChannelNodeStructureEntities;
        private List<StructureEntity> _allResourceStructureEntities;
        private Dictionary<int, Entity> _cachedParentEntities;

        public EntityService(IConfiguration config, EpiMappingHelper mappingHelper)
        {
            _config = config;
            _mappingHelper = mappingHelper;

            _cachedEntities = new List<Entity>();
            _cachedChannelNodeStructureEntities = new Dictionary<string, List<StructureEntity>>();
            _cachedParentEntities = new Dictionary<int, Entity>();
        }

        public Entity GetEntity(int id, LoadLevel loadLevel)
        {
            var existingEntity = _cachedEntities.FirstOrDefault(x => x.Id == id);

            if (existingEntity != null && loadLevel <= existingEntity.LoadLevel)
                return existingEntity;

            var fetchedEntity = RemoteManager.DataService.GetEntity(id, loadLevel);

            if (existingEntity != null)
                _cachedEntities.Remove(existingEntity);

            _cachedEntities.Add(fetchedEntity);

            return fetchedEntity;
        }

        public List<StructureEntity> GetAllStructureEntitiesInChannel(List<EntityType> entityTypes)
        {
            List<StructureEntity> result = new List<StructureEntity>();
            foreach (EntityType entityType in entityTypes)
            {
                List<StructureEntity> response = RemoteManager.ChannelService.GetAllChannelStructureEntitiesForType(_config.ChannelId, entityType.Id);
                result.AddRange(response);
            }

            return _config.ForceIncludeLinkedContent ?
                        result.ToList() :
                        result.Where(x => FilterLinkedContentNotBelongingToChannelNode(x, result)).ToList();
        }

        public List<StructureEntity> GetAllResourceLocations(int resourceEntityId)
        {
            if(_allResourceStructureEntities == null)
                _allResourceStructureEntities = RemoteManager.ChannelService.GetAllChannelStructureEntitiesForType(_config.ChannelId, "Resource");

            return _allResourceStructureEntities.Where(x => x.EntityId == resourceEntityId).ToList();
        }

        public List<StructureEntity> GetEntityInChannelWithParent(int channelId, int entityId, int parentId)
        {
            var result = new List<StructureEntity>();
            var response = RemoteManager.ChannelService.GetAllStructureEntitiesForEntityWithParentInChannel(channelId, entityId, parentId);
            if (response.Any())
            {
                result.AddRange(response);
            }

            return result;
        }

        public string GetTargetEntityPath(int targetEntityId, List<StructureEntity> channelEntities, int? parentId = null)
        {
            StructureEntity targetStructureEntity = new StructureEntity();

            if (parentId == null)
            {
                targetStructureEntity = channelEntities.Find(i => i.EntityId.Equals(targetEntityId));
            }
            else
            {
                targetStructureEntity = channelEntities.Find(i => i.EntityId.Equals(targetEntityId) && i.ParentId.Equals(parentId));
            }


            string path = string.Empty;

            if (targetStructureEntity != null)
            {
                path = targetStructureEntity.Path;
            }

            return path;
        }

        public List<StructureEntity> GetChildrenEntitiesInChannel(int entityId, string path)
        {
            var result = new List<StructureEntity>();
            if (!string.IsNullOrEmpty(path))
            {
                var response = RemoteManager.ChannelService.GetChannelStructureChildrenFromPath(entityId, path);
                if (response.Any())
                {
                    result.AddRange(response);
                }
            }

            return result;
        }

        public List<StructureEntity> GetStructureEntitiesForEntityInChannel(int channelId, int entityId)
        {
            return RemoteManager.ChannelService.GetAllStructureEntitiesForEntityInChannel(channelId, entityId);
        }

        public StructureEntity GetParentStructureEntity(int channelId, int sourceEntityId, int targetEntityId, List<StructureEntity> channelEntities)
        {
            var targetStructureEntity = channelEntities.Find(i => i.EntityId.Equals(targetEntityId) && i.ParentId.Equals(sourceEntityId));
            var structureEntities = RemoteManager.ChannelService.GetAllStructureEntitiesForEntityInChannel(channelId, sourceEntityId);

            if (targetStructureEntity == null || !structureEntities.Any())
            {
                return null;
            }

            int endIndex = targetStructureEntity.Path.LastIndexOf("/", StringComparison.InvariantCulture);

            string parentPath = targetStructureEntity.Path.Substring(0, endIndex);

            return structureEntities.Find(i => i.Path.Equals(parentPath) && i.EntityId.Equals(sourceEntityId));
        }

        public List<StructureEntity> GetChannelNodeStructureEntitiesInPath(string path)
        {
            if (_cachedChannelNodeStructureEntities.ContainsKey(path))
                return _cachedChannelNodeStructureEntities[path];

            var structureEntities = RemoteManager.ChannelService.GetAllChannelStructureEntitiesForTypeInPath(path, "ChannelNode");
            _cachedChannelNodeStructureEntities.Add(path, structureEntities);
            return structureEntities;
        }


        public Entity GetParentProduct(StructureEntity itemStructureEntity)
        {
            var entityId = itemStructureEntity.EntityId;

            if (_cachedParentEntities.ContainsKey(entityId))
                return _cachedParentEntities[entityId];

            var inboundLinks = RemoteManager.DataService.GetInboundLinksForEntity(entityId);
            var relationLink = inboundLinks.FirstOrDefault(x => _mappingHelper.IsRelation(x.LinkType));

            if (relationLink == null)
                return null;

            var parent = GetEntity(relationLink.Source.Id, LoadLevel.DataOnly);
            _cachedParentEntities.Add(entityId, parent);

            return parent;
        }

        public void FlushCache()
        {
            _cachedEntities = new List<Entity>();
            _cachedChannelNodeStructureEntities = new Dictionary<string, List<StructureEntity>>();
            _allResourceStructureEntities = null;
        }
        

        /// <summary>
        /// Tells you whether or not a structure entity belongs in the channel, based on it's links.
        /// True for any entity that has a direct relation with either a product/bundle/package, or a channel node. False for 
        /// anything that's ONLY included in the channel as upsell/accessories and the like (typically item-item-links or product-product-links).
        /// </summary>
        /// <param name="structureEntity">The StructureEntity to query.</param>
        /// <param name="allStructureEntities">Everything in the channel.</param>
        private bool FilterLinkedContentNotBelongingToChannelNode(StructureEntity structureEntity, List<StructureEntity> allStructureEntities)
        {
            var sameEntityStructureEntities = allStructureEntities.Where(x => x.EntityId == structureEntity.EntityId);
            return sameEntityStructureEntities.Any(BelongsInChannel);
        }

        private bool BelongsInChannel(StructureEntity arg)
        {
            var isRelation = _mappingHelper.IsRelation(arg.LinkTypeIdFromParent);
            var isChannelNodeLink = _mappingHelper.IsChannelNodeLink(arg.LinkTypeIdFromParent);

            return isRelation || isChannelNodeLink;
        }
    }
}