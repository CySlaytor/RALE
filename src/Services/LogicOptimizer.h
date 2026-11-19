#pragma once

#include <memory>
#include "../Models/AssetModels.h"
#include "../Models/LogicModels.h"

namespace RALE {

class LogicOptimizer
{
public:
    int OptimizeAsset(std::shared_ptr<IAssetItem> asset);
    int PadAsset(std::shared_ptr<IAssetItem> asset);
    bool IsFullyOptimized(std::shared_ptr<IAssetItem> asset);

private:
    bool HasLeadingZeros(const Operand &op);
    bool OptimizeOperand(Operand &op);
    bool PadOperand(Operand &op);
};

} // namespace RALE
