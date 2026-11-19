#pragma once

#include <memory>
#include <QList>
#include "ProjectMetadataService.h"
#include "../Models/AssetModels.h"
#include "../Models/RegionConfig.h"

namespace RALE {

class MultiRegionBatchProcessor
{
private:
    ProjectMetadataService &_metadata;

public:
    explicit MultiRegionBatchProcessor(ProjectMetadataService &metadata)
        : _metadata(metadata)
    {
    }

    void ApplyMultiRegionToAsset(
        std::shared_ptr<IAssetItem> localItem,
        const RegionConfig *baseConfig,
        const QList<RegionConfig> &targetConfigs);

private:
    void ApplyAddressSwap(Operand &op, const QList<AddressRangeMap> &ranges);
};

} // namespace RALE
