﻿namespace inRiver.EPiServerCommerce.Importer
{
    public interface ICatalogImporter
    {
        void DeleteCatalogEntry(string code);

        void DeleteCatalog(int catalogId);
    }
}