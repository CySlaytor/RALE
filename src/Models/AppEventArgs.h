#pragma once

#include <QString>
#include <QList>
#include <cstdint>

namespace RALogicEditor {

class PointerChainArgs
{
public:
    QString BaseAddress;
    QList<int64_t> Offsets;

    PointerChainArgs(const QString &baseAddr, const QList<int64_t> &offsets)
        : BaseAddress(baseAddr), Offsets(offsets)
    {
    }
};

} // namespace RALogicEditor
