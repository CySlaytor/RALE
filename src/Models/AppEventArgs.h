#pragma once

#include <QList>
#include <QString>

#include <cstdint>

namespace RALE {

class PointerChainArgs {
  public:
    QString BaseAddress;
    QList<int64_t> Offsets;

    PointerChainArgs(const QString& baseAddr, const QList<int64_t>& offsets) : BaseAddress(baseAddr), Offsets(offsets) {}
};

} // namespace RALE
