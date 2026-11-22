#pragma once

#include <QList>
#include <QString>

#include <cstdint>
#include <functional>

namespace RALE {

struct ConsoleMemRegion {
    int64_t Start = 0;
    int64_t End = 0;
    QString Name;
    bool IsError = false;
    std::function<int64_t(int64_t)> Transform = nullptr;
};

struct ConsoleInfo {
    int Id = 0;
    QString Name;
    QList<ConsoleMemRegion> Regions;
};

class ConsoleRegions {
  public:
    static const ConsoleInfo* GetConsoleInfo(int consoleId);
};

} // namespace RALE
