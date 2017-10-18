﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Epinova.InRiverConnector.EpiserverAdapter.Helpers;
using inRiver.Integration.Logging;
using inRiver.Remoting;
using inRiver.Remoting.Log;
using inRiver.Remoting.Objects;

namespace Epinova.InRiverConnector.EpiserverAdapter.EpiXml
{
    public class EpiElementFactory
    {
        private readonly Configuration _config;

        public EpiElementFactory(Configuration config)
        {
            _config = config;
        }

        public XElement InRiverEntityTypeToMetaClass(string name, string entityTypeName)
        {
            return new XElement(
                "MetaClass",
                new XElement("Namespace", "Mediachase.Commerce.Catalog.User"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("MetaClassType", "User"),
                new XElement("ParentClass", EpiMappingHelper.GetParentClassForEntityType(entityTypeName)),
                new XElement("TableName", EpiMappingHelper.GetTableNameForEntityType(entityTypeName, name)),
                new XElement("Description", "From inRiver"),
                new XElement("IsSystem", "False"),
                new XElement("IsAbstract", "False"),
                new XElement("FieldListChangedSqlScript"),
                new XElement("Tag"),
                new XElement("Attributes"));
        }

        public XElement InRiverFieldTypeToMetaField(FieldType fieldType, Configuration config)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", EpiMappingHelper.GetEpiserverFieldName(fieldType, config)),
                new XElement("FriendlyName", EpiMappingHelper.GetEpiserverFieldName(fieldType, config)),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", EpiMappingHelper.GetEpiserverDataType(fieldType, config)),
                new XElement("Length", EpiMappingHelper.GetMetaFieldLength(fieldType, config)),
                new XElement("AllowNulls", !fieldType.Mandatory),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", BusinessHelper.GetAllowSearch(fieldType)),
                new XElement("MultiLanguageValue", BusinessHelper.FieldTypeIsMultiLanguage(fieldType)),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                new XElement(
                    "Attributes",
                    new XElement(
                        "Attribute",
                        new XElement("Key", "useincomparing"),
                        new XElement("Value", BusinessHelper.FieldIsUseInCompare(fieldType)))),
                new XElement("OwnerMetaClass", fieldType.EntityTypeId));
        }

        public XElement EPiMustHaveMetaField(string name)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", "LongString"),
                new XElement("Length", 150),
                new XElement("AllowNulls", "True"),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", "True"),
                new XElement("MultiLanguageValue", "True"),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                new XElement(
                    "Attributes",
                    new XElement("Attribute", new XElement("Key", "useincomparing"), new XElement("Value", "True"))));
        }

        public XElement EPiSpecificationField(string name)
        {
            return new XElement(
                "MetaField",
                new XElement("Namespace", "Mediachase.Commerce.Catalog"),
                new XElement("Name", name),
                new XElement("FriendlyName", name),
                new XElement("Description", "From inRiver"),
                new XElement("DataType", "LongHtmlString"),
                new XElement("Length", 65000),
                new XElement("AllowNulls", "True"),
                new XElement("SaveHistory", "False"),
                new XElement("AllowSearch", "True"),
                new XElement("MultiLanguageValue", "True"),
                new XElement("IsSystem", "False"),
                new XElement("Tag"),
                new XElement(
                    "Attributes",
                    new XElement("Attribute", new XElement("Key", "useincomparing"), new XElement("Value", "False"))));
        }

        public XElement CreateAssociationTypeElement(LinkType linkType)
        {
            return new XElement(
                "AssociationType",
                new XElement("TypeId", linkType.Id),
                new XElement("Description", linkType.Id));
        }

        public XElement CreateCatalogElement(Entity channel, Configuration config)
        {
            return new XElement(
                "Catalog",
                new XAttribute("name", EpiMappingHelper.GetNameForEntity(channel, config, 100)),
                new XAttribute("lastmodified", channel.LastModified.ToString("O")),
                new XAttribute("startDate", BusinessHelper.GetStartDateFromEntity(channel)),
                new XAttribute("endDate", BusinessHelper.GetEndDateFromEntity(channel)),
                new XAttribute("defaultCurrency", config.ChannelDefaultCurrency),
                new XAttribute("weightBase", config.ChannelDefaultWeightBase),
                new XAttribute("defaultLanguage", config.ChannelDefaultLanguage.Name.ToLower()),
                new XAttribute("sortOrder", 0),
                new XAttribute("isActive", "True"),
                new XAttribute(
                    "languages",
                    string.Join(",", BusinessHelper.CultureInfosToStringArray(config.LanguageMapping.Keys.ToArray()))));
        }

        public XElement CreateNodeElement(Entity entity, string parentId, int sortOrder, Configuration config)
        {
            return new XElement(
                "Node",
                new XElement("Name", EpiMappingHelper.GetNameForEntity(entity, config, 100)),
                new XElement("StartDate", BusinessHelper.GetStartDateFromEntity(entity)),
                new XElement("EndDate", BusinessHelper.GetEndDateFromEntity(entity)),
                new XElement("IsActive", !entity.EntityType.IsLinkEntityType),
                new XElement("SortOrder", sortOrder),
                new XElement("DisplayTemplate", BusinessHelper.GetDisplayTemplateEntity(entity)),
                new XElement("Guid", GetChannelEntityGuid(config.ChannelId, entity.Id)),
                new XElement("Code", ChannelPrefixHelper.GetEpiserverCode(entity.Id, config)),
                new XElement(
                    "MetaData",
                    new XElement("MetaClass", new XElement("Name", GetMetaClassForEntity(entity))),
                    new XElement(
                        "MetaFields",
                        GetDisplayXXElement(entity.DisplayName, "DisplayName", config),
                        GetDisplayXXElement(entity.DisplayDescription, "DisplayDescription", config),
                        from f in entity.Fields
                        where !f.IsEmpty() && !EpiMappingHelper.SkipField(f.FieldType, config)
                        select GetMetaFieldValueElement(f, config))),
                new XElement(
                    "ParentNode",
                    string.IsNullOrEmpty(parentId) ? null : ChannelPrefixHelper.GetEpiserverCode(parentId, config)),
                CreateSEOInfoElement(entity, config));
        }

        public XElement CreateSEOInfoElement(Entity entity, Configuration config)
        {
            XElement seoInfo = new XElement("SeoInfo");
            foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
            {
                string uri = BusinessHelper.GetSeoUriFromEntity(entity, culturePair.Value, config);
                string title = BusinessHelper.GetSeoTitleFromEntity(entity, culturePair.Value);
                string description = BusinessHelper.GetSeoDescriptionFromEntity(entity, culturePair.Value);
                string keywords = BusinessHelper.GetSeoKeywordsFromEntity(entity, culturePair.Value);
                string urisegment = BusinessHelper.GetSeoUriSegmentFromEntity(entity, culturePair.Value, config);

                if (string.IsNullOrEmpty(uri) && string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description)
                    && string.IsNullOrEmpty(keywords) && string.IsNullOrEmpty(urisegment))
                {
                    continue;
                }

                seoInfo.Add(
                    new XElement(
                        "Seo",
                        new XElement("LanguageCode", culturePair.Key.Name.ToLower()),
                        new XElement("Uri", uri),
                        new XElement("Title", title),
                        new XElement("Description", description),
                        new XElement("Keywords", keywords),
                        new XElement("UriSegment", urisegment)));
            }

            return seoInfo;
        }
        
        public XElement InRiverEntityToEpiEntry(Entity entity, Configuration config)
        {
            return new XElement(
                "Entry",
                new XElement("Name", EpiMappingHelper.GetNameForEntity(entity, config, 100)),
                new XElement("StartDate", BusinessHelper.GetStartDateFromEntity(entity)),
                new XElement("EndDate", BusinessHelper.GetEndDateFromEntity(entity)),
                new XElement("IsActive", "True"),
                new XElement("DisplayTemplate", BusinessHelper.GetDisplayTemplateEntity(entity)),
                new XElement("Code", ChannelPrefixHelper.GetEpiserverCode(entity.Id, config)),
                new XElement("EntryType", EpiMappingHelper.GetEntryType(entity.EntityType.Id, config)),
                new XElement("Guid", GetChannelEntityGuid(config.ChannelId, entity.Id)),
                new XElement(
                    "MetaData",
                    new XElement("MetaClass", new XElement("Name", GetMetaClassForEntity(entity))),
                    new XElement(
                        "MetaFields",
                        GetDisplayXXElement(entity.DisplayName, "DisplayName", config),
                        GetDisplayXXElement(entity.DisplayDescription, "DisplayDescription", config),
                        from f in entity.Fields
                        where UseField(entity, f) && !EpiMappingHelper.SkipField(f.FieldType, config)
                        select GetMetaFieldValueElement(f, config))),
                        CreateSEOInfoElement(entity, config)
                        );
        }

        private Guid GetChannelEntityGuid(int channelId, int entityId)
        {
            var concatIds = channelId.ToString().PadLeft(16, '0') + entityId.ToString().PadLeft(16, '0');
            return new Guid(concatIds);
        }

        public XElement GetMetaFieldValueElement(Field field, Configuration config)
        {
            XElement metaField = new XElement(
                "MetaField",
                new XElement("Name", EpiMappingHelper.GetEpiserverFieldName(field.FieldType, config)),
                new XElement("Type", EpiMappingHelper.GetEpiserverDataType(field.FieldType, config)));

           
            if (field.FieldType.DataType.Equals(DataType.LocaleString))
            {
                LocaleString ls = field.Data as LocaleString;
                if (!field.IsEmpty())
                {
                    foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                    {
                        if (ls != null)
                        {
                            metaField.Add(
                                new XElement(
                                    "Data",
                                    new XAttribute("language", culturePair.Key.Name.ToLower()),
                                    new XAttribute("value", ls[culturePair.Value] ?? string.Empty)));
                        }
                    }
                }
                else
                {
                    foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in config.LanguageMapping)
                    {
                        metaField.Add(
                            new XElement(
                                "Data",
                                new XAttribute("language", culturePair.Key.Name.ToLower()),
                                new XAttribute("value", string.Empty)));
                    }
                }
            }
            else if (field.FieldType.DataType.Equals(DataType.CVL))
            {
                metaField.Add(
                    new XElement(
                        "Data", 
                        BusinessHelper.GetCVLValues(field, config)));
            }
            else
            {
                metaField.Add(
                new XElement(
                    "Data",
                    new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                    new XAttribute("value", BusinessHelper.GetFlatFieldData(field, config))));
            }

            return metaField;
        }

        public XElement CreateSimpleMetaFieldElement(string name, string value, Configuration config)
        {
            return new XElement(
                "MetaField",
                new XElement("Name", name),
                new XElement("Type", "ShortString"),
                new XElement("Data",
                    new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                    new XAttribute("value", value)));
        }

        public XElement CreateNodeEntryRelationElement(string sourceId, string targetId, int sortOrder, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            return new XElement("NodeEntryRelation",
                new XElement("EntryCode", ChannelPrefixHelper.GetEpiserverCode(targetId, config)),
                new XElement("NodeCode", ChannelPrefixHelper.GetEpiserverCode(sourceId, config)),
                new XElement("SortOrder", sortOrder));
        }

        public XElement CreateNodeRelationElement(string sourceId, string targetId, int sortOrder, Configuration config)
        {
            return new XElement("NodeRelation",
                new XElement("ChildNodeCode", ChannelPrefixHelper.GetEpiserverCode(targetId, config)),
                new XElement("ParentNodeCode", ChannelPrefixHelper.GetEpiserverCode(sourceId, config)),
                new XElement("SortOrder", sortOrder));
        }

        public XElement CreateEntryRelationElement(string sourceId, string parentEntityType, string targetId, int sortOrder, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            string relationType = "ProductVariation";

            if (!string.IsNullOrEmpty(parentEntityType))
            {
                string sourceType = EpiMappingHelper.GetEntryType(parentEntityType, config);

                // Change it if needed.
                switch (sourceType)
                {
                    case "Package":
                    case "DynamicPackage":
                        relationType = "PackageEntry";
                        break;
                    case "Bundle":
                        relationType = "BundleEntry";
                        break;
                }
            }

            return new XElement(
                "EntryRelation",
                new XElement("ParentEntryCode", ChannelPrefixHelper.GetEpiserverCode(sourceId, config)),
                new XElement("ChildEntryCode", ChannelPrefixHelper.GetEpiserverCode(targetId, config)),
                new XElement("RelationType", relationType),
                new XElement("Quantity", 0),
                new XElement("GroupName", "default"),
                new XElement("SortOrder", sortOrder));
        }

        public XElement CreateCatalogAssociationElement(StructureEntity structureEntity, Entity linkEntity, Configuration config, Dictionary<int, Entity> channelEntities = null)
        {
            // Unique Name with no spaces required for EPiServer Commerce
            string name = EpiMappingHelper.GetAssociationName(structureEntity, linkEntity, config);
            string description = structureEntity.LinkEntityId == null ? structureEntity.LinkTypeIdFromParent : ChannelPrefixHelper.GetEpiserverCode(structureEntity.LinkEntityId.Value, config);
            description = description ?? string.Empty;

            return new XElement(
                "CatalogAssociation",
                new XElement("Name", name),
                new XElement("Description", description),
                new XElement("SortOrder", structureEntity.SortOrder),
                new XElement("EntryCode", ChannelPrefixHelper.GetEpiserverCode(structureEntity.ParentId, config)),
                CreateAssociationElement(structureEntity, config));
        }

        public XElement CreateAssociationElement(StructureEntity structureEntity, Configuration config)
        {
            return new XElement(
                "Association",
                new XElement("EntryCode", ChannelPrefixHelper.GetEpiserverCode(structureEntity.EntityId, config)),
                new XElement("SortOrder", structureEntity.SortOrder),
                    new XElement("Type", structureEntity.LinkTypeIdFromParent));
        }


        public XElement CreateResourceMetaFieldsElement(EntityType resourceType, Configuration config)
        {
            return new XElement(
                "ResourceMetaFields",
                resourceType.FieldTypes.Select(
                    fieldtype =>
                    new XElement(
                        "ResourceMetaField",
                        new XElement("FieldName", EpiMappingHelper.GetEpiserverFieldName(fieldtype, config)),
                        new XElement("FriendlyName", EpiMappingHelper.GetEpiserverFieldName(fieldtype, config)),
                        new XElement("Description", EpiMappingHelper.GetEpiserverFieldName(fieldtype, config)),
                        new XElement("FieldType", EpiMappingHelper.GetEpiserverDataType(fieldtype, config)),
                        new XElement("Format", "Text"),
                        new XElement("MaximumLength", EpiMappingHelper.GetMetaFieldLength(fieldtype, config)),
                        new XElement("AllowNulls", !fieldtype.Mandatory),
                        new XElement("UniqueValue", fieldtype.Unique))));
        }

        public XElement GetMetaClassesFromFieldSets(Configuration config)
        {
            List<XElement> metaClasses = new List<XElement>();
            List<XElement> metafields = new List<XElement>();

            XElement diaplyNameElement = EPiMustHaveMetaField("DisplayName");
            XElement displayDescriptionElement = EPiMustHaveMetaField("DisplayDescription");
            XElement specification = EPiSpecificationField("SpecificationField");
            bool addSpec = false;

            foreach (EntityType entityType in config.ExportEnabledEntityTypes)
            {
                if (entityType.LinkTypes.Find(a => a.TargetEntityTypeId == "Specification") != null && entityType.Id != "Specification")
                {
                    specification.Add(new XElement("OwnerMetaClass", entityType.Id));
                    foreach (FieldSet fieldSet in entityType.FieldSets)
                    {
                        string name = entityType.Id + "_" + fieldSet.Id;
                        specification.Add(new XElement("OwnerMetaClass", name));
                    }

                    addSpec = true;
                }

                Dictionary<string, List<XElement>> fieldTypesFieldSets = new Dictionary<string, List<XElement>>();
                metaClasses.Add(InRiverEntityTypeToMetaClass(entityType.Id, entityType.Id));
                foreach (FieldSet fieldset in entityType.FieldSets)
                {
                    string name = entityType.Id + "_" + fieldset.Id;
                    metaClasses.Add(InRiverEntityTypeToMetaClass(name, entityType.Id));
                    foreach (string fieldTypeName in fieldset.FieldTypes)
                    {
                        if (!fieldTypesFieldSets.ContainsKey(fieldTypeName))
                        {
                            fieldTypesFieldSets.Add(fieldTypeName, new List<XElement> { new XElement("OwnerMetaClass", name) });
                        }
                        else
                        {
                            fieldTypesFieldSets[fieldTypeName].Add(new XElement("OwnerMetaClass", name));
                        }
                    }

                    diaplyNameElement.Add(new XElement("OwnerMetaClass", name));
                    displayDescriptionElement.Add(new XElement("OwnerMetaClass", name));
                }

                diaplyNameElement.Add(new XElement("OwnerMetaClass", entityType.Id));
                displayDescriptionElement.Add(new XElement("OwnerMetaClass", entityType.Id));
                foreach (FieldType fieldType in entityType.FieldTypes)
                {
                    if (EpiMappingHelper.SkipField(fieldType, config))
                    {
                        continue;
                    }

                    XElement metaField = InRiverFieldTypeToMetaField(fieldType, config);

                    if (fieldTypesFieldSets.ContainsKey(fieldType.Id))
                    {
                        foreach (XElement element in fieldTypesFieldSets[fieldType.Id])
                        {
                            metaField.Add(element);
                        }
                    }
                    else
                    {
                        foreach (FieldSet fieldSet in entityType.FieldSets)
                        {
                            string name = entityType.Id + "_" + fieldSet.Id;
                            metaField.Add(new XElement("OwnerMetaClass", name));
                        }
                    }

                    if (metafields.Any(mf =>
                    {
                        XElement nameElement = mf.Element("Name");
                        return nameElement != null && nameElement.Value.Equals(EpiMappingHelper.GetEpiserverFieldName(fieldType, config));
                    }))
                    {
                        XElement existingMetaField = metafields.FirstOrDefault(mf =>
                        {
                            XElement nameElement = mf.Element("Name");
                            return nameElement != null && nameElement.Value.Equals(EpiMappingHelper.GetEpiserverFieldName(fieldType, config));
                        });
                        if (existingMetaField != null)
                        {
                            var movefields = metaField.Elements("OwnerMetaClass");
                            existingMetaField.Add(movefields);
                        }
                    }
                    else
                    {
                        metafields.Add(metaField);
                    }
                }
            }

            metafields.Add(diaplyNameElement);
            metafields.Add(displayDescriptionElement);
            if (addSpec)
            {
                metafields.Add(specification);
            }

            return new XElement("MetaDataPlusBackup", new XAttribute("version", "1.0"), metaClasses.ToArray(), metafields.ToArray());
        }

        public List<XElement> GenerateSkuItemElemetsFromItem(Entity item, Configuration configuration)
        {
            XDocument skuDoc = SkuFieldToDocument(item, configuration);
            if (skuDoc.Root == null || skuDoc.Element("SKUs") == null)
            {
                return new List<XElement>();
            }

            Link specLink = item.OutboundLinks.Find(l => l.Target.EntityType.Id == "Specification");
            XElement specificationMetaField = null;
            if (specLink != null)
            {
                specificationMetaField = new XElement(
                    "MetaField",
                    new XElement("Name", "SpecificationField"),
                    new XElement("Type", "LongHtmlString"));
                foreach (KeyValuePair<CultureInfo, CultureInfo> culturePair in configuration.LanguageMapping)
                {
                    string htmlData = RemoteManager.DataService.GetSpecificationAsHtml(
                        specLink.Target.Id,
                        item.Id,
                        culturePair.Value);
                    specificationMetaField.Add(
                        new XElement(
                            "Data",
                            new XAttribute("language", culturePair.Key.Name.ToLower()),
                            new XAttribute("value", htmlData)));
                }
            }

            List<XElement> skuElements = new List<XElement>();
            XElement skuElement = skuDoc.Element("SKUs");
            if (skuElement != null)
            {
                foreach (XElement sku in skuElement.Elements())
                {
                    string id = sku.Attribute("id").Value;
                    if (string.IsNullOrEmpty(id))
                    {
                        IntegrationLogger.Write(
                            LogLevel.Information,
                            string.Format("Could not find the id for the SKU data for item: {0}", item.Id));
                        continue;
                    }

                    XElement itemElement = InRiverEntityToEpiEntry(item, configuration);
                    XElement nameElement = sku.Element("Name");
                    if (nameElement != null)
                    {
                        string name = (!string.IsNullOrEmpty(nameElement.Value)) ? nameElement.Value : id;
                        XElement itemElementName = itemElement.Element("Name");
                        if (itemElementName != null)
                        {
                            itemElementName.Value = name;
                        }
                    }

                    XElement codeElement = itemElement.Element("Code");
                    if (codeElement != null)
                    {
                        codeElement.Value = ChannelPrefixHelper.GetEpiserverCode(id, configuration);
                    }

                    XElement entryTypeElement = itemElement.Element("EntryType");
                    if (entryTypeElement != null)
                    {
                        entryTypeElement.Value = "Variation";
                    }

                    XElement skuDataElement = sku.Element(Configuration.SKUData);
                    if (skuDataElement != null)
                    {
                        foreach (XElement skuData in skuDataElement.Elements())
                        {
                            XElement metaDataElement = itemElement.Element("MetaData");
                            if (metaDataElement != null && metaDataElement.Element("MetaFields") != null)
                            {
                                // ReSharper disable once PossibleNullReferenceException
                                metaDataElement.Element("MetaFields").Add(CreateSimpleMetaFieldElement(skuData.Name.LocalName, skuData.Value, configuration));
                            }
                        }
                    }

                    if (specificationMetaField != null)
                    {
                        XElement metaDataElement = itemElement.Element("MetaData");
                        if (metaDataElement != null && metaDataElement.Element("MetaFields") != null)
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            metaDataElement.Element("MetaFields").Add(specificationMetaField);
                        }
                    }

                    skuElements.Add(itemElement);
                }
            }

            return skuElements;
        }

        public XDocument SkuFieldToDocument(Entity item, Configuration configuration)
        {
            Field skuField = item.GetField(Configuration.SKUFieldName);
            if (skuField == null || skuField.Data == null)
            {
                XElement itemElement = InRiverEntityToEpiEntry(item, configuration);
                IntegrationLogger.Write(
                    LogLevel.Information,
                    string.Format("Could not find SKU data for item: {0}", item.Id));
                return new XDocument(itemElement);
            }

            return XDocument.Parse(skuField.Data.ToString());
        }

        public List<string> SkuItemIds(Entity item, Configuration configuration)
        {
            Field skuField = item.GetField(Configuration.SKUFieldName);
            if (skuField == null || skuField.IsEmpty())
            {
                return new List<string> { item.Id.ToString(CultureInfo.InvariantCulture) };
            }

            XDocument skuDoc = SkuFieldToDocument(item, configuration);

            XElement skusElement = skuDoc.Element("SKUs");
            if (skusElement != null)
            {
                return
                    (from skuElement in skusElement.Elements()
                     where skuElement.HasAttributes
                     select skuElement.Attribute("id").Value).ToList();
            }

            return new List<string>();
        }

        // ReSharper disable once InconsistentNaming
        private XElement GetDisplayXXElement(Field displayField, string name, Configuration config)
        {
            if (displayField == null || displayField.IsEmpty())
            {
                return new XElement(
                    "MetaField",
                    new XElement("Name", name),
                    new XElement("Type", "LongHtmlString"),
                    new XElement(
                        "Data",
                        new XAttribute("language", config.ChannelDefaultLanguage.Name.ToLower()),
                        new XAttribute("value", string.Empty)));
            }

            XElement element = GetMetaFieldValueElement(displayField, config);
            XElement nameElement = element.Element("Name");
            if (nameElement != null)
            {
                nameElement.Value = name;
            }

            XElement typeElement = element.Element("Type");
            if (typeElement != null)
            {
                typeElement.Value = "LongHtmlString";
            }

            return element;
        }

        private bool UseField(Entity entity, Field field)
        {
            if (!field.FieldType.ExcludeFromDefaultView)
            {
                return true;
            }

            List<FieldSet> otherFieldSets = entity.EntityType.FieldSets.Where(fs => !fs.Id.Equals(entity.FieldSetId)).ToList();
            if (otherFieldSets.Count == 0)
            {
                return true;
            }

            FieldSet fieldSet = entity.EntityType.FieldSets.Find(fs => fs.Id.Equals(entity.FieldSetId));
            if (fieldSet != null)
            {
                if (fieldSet.FieldTypes.Contains(field.FieldType.Id))
                {
                    return true;
                }
            }

            foreach (FieldSet fs in otherFieldSets)
            {
                if (fs.FieldTypes.Contains(field.FieldType.Id))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetMetaClassForEntity(Entity entity)
        {
            if (!string.IsNullOrEmpty(entity.FieldSetId) && entity.EntityType.FieldSets.Any(fs => fs.Id == entity.FieldSetId))
            {
                return entity.EntityType.Id + "_" + entity.FieldSetId;
            }

            return entity.EntityType.Id;
        }
    }
}