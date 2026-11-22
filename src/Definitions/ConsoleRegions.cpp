#include "ConsoleRegions.h"

#include <QMap>

namespace RALE {

namespace {

QMap<int, ConsoleInfo> BuildConsoleMap() {
    QMap<int, ConsoleInfo> map;

    auto add = [&map](int id, const QString& name, QList<ConsoleMemRegion> regions = {}) {
        map.insert(id, ConsoleInfo{id, name, std::move(regions)});
    };

    // Nintendo
    add(4, "Game Boy",
        {
            {0x0000, 0x7FFF, "ROM Data", true, nullptr},
            {0x8000, 0x9FFF, "Graphics Data", false, nullptr},
            {0xE000, 0xFDFF, "Echo RAM", true, [](int64_t x) { return x - 0x2000; }},
            {0xFE00, 0xFE9F, "Graphics Data", false, nullptr},
            {0xFEA0, 0xFF7F, "Unusable Memory", true, nullptr},
        });
    add(6, "Game Boy Color",
        {
            {0x0000, 0x3FFF, "ROM Bank", true, nullptr},
            {0x4000, 0x7FFF, "Switchable ROM Bank", true, nullptr},
            {0x8000, 0x9FFF, "Graphics Data", false, nullptr},
            {0xE000, 0xFDFF, "Echo RAM", true, [](int64_t x) { return x - 0x2000; }},
            {0xFE00, 0xFE9F, "Graphics Data", false, nullptr},
            {0xFEA0, 0xFF7F, "Unusable Memory", true, nullptr},
        });
    add(5, "Game Boy Advance");
    QList<ConsoleMemRegion> nesRegions = {
        {0x0100, 0x01FF, "(possible) Stack", false, nullptr},
        {0x0800, 0x0FFF, "Mirror RAM", true, [](int64_t x) { return x - 0x0800; }},
        {0x1000, 0x17FF, "Mirror RAM", true, [](int64_t x) { return x - 0x1000; }},
        {0x1800, 0x1FFF, "Mirror RAM", true, [](int64_t x) { return x - 0x1800; }},
        {0x2008, 0x3FFF, "Mirrored PPU Registers", true, [](int64_t x) { return 0x2000 + (x % 8); }},
    };
    add(7, "NES/Famicom", nesRegions);
    add(81, "Famicom Disk System", nesRegions);
    add(3, "SNES/Super Famicom");
    add(2, "Nintendo 64");
    add(16, "GameCube");
    add(19, "Wii");
    add(18, "Nintendo DS",
        {
            {0x0400000, 0x0FFFFFF, "Unused", true, nullptr},
        });
    add(78, "Nintendo DSi");
    add(24, "Pokemon Mini",
        {
            {0x0000, 0x0FFF, "BIOS RAM", false, nullptr},
        });
    add(28, "Virtual Boy");

    // Sony
    add(12, "PlayStation",
        {
            {0x0000000, 0x000FFFF, "Kernel RAM", true, nullptr},
        });
    add(21, "PlayStation 2",
        {
            {0x0000000, 0x00FFFFF, "Kernel RAM", true, nullptr},
            {0x1FE0000, 0x1FFFFFF, "Probable Stack", false, nullptr},
        });
    add(41, "PlayStation Portable",
        {
            {0x0000000, 0x07FFFFF, "Kernel RAM", true, nullptr},
        });

    // Atari
    add(25, "Atari 2600");
    add(51, "Atari 7800",
        {
            {0x002800, 0x002FFF, "Mirror RAM", true, [](int64_t x) { return x - 0x0800; }},
            {0x003000, 0x0037FF, "Mirror RAM", true, [](int64_t x) { return x - 0x1000; }},
            {0x003800, 0x003FFF, "Mirror RAM", true, [](int64_t x) { return x - 0x1800; }},
        });
    add(17, "Atari Jaguar");
    add(77, "Atari Jaguar CD");
    add(13, "Atari Lynx");

    // Sega
    add(33, "SG-1000",
        {
            {0x00C400, 0x00FFFF, "System RAM Mirror", true, [](int64_t x) { return 0xC000 + (x % 0x400); }},
        });
    add(11, "Master System",
        {
            {0x00E000, 0x00FFFF, "System RAM Mirror", true, [](int64_t x) { return x - 0x2000; }},
        });
    add(15, "Game Gear",
        {
            {0x00E000, 0x00FFFF, "System RAM Mirror", true, [](int64_t x) { return x - 0x2000; }},
        });
    add(1, "Genesis/Mega Drive");
    add(9, "Sega CD");
    add(10, "32X");
    add(39, "Saturn");
    add(40, "Dreamcast");

    // NEC
    add(8, "PC Engine/TurboGrafx-16");
    add(76, "PC Engine CD/TurboGrafx-CD");
    add(47, "PC-8000/8800");
    add(49, "PC-FX");

    // SNK
    add(56, "Neo Geo CD");
    add(14, "Neo Geo Pocket");

    // Other
    add(43, "3DO Interactive Multiplayer");
    add(37, "Amstrad CPC");
    add(38, "Apple II");
    add(27, "Arcade");
    add(73, "Arcadia 2001");
    add(71, "Arduboy");
    add(44, "ColecoVision");
    add(75, "Elektor TV Games Computer",
        {
            {0x001400, 0x0014FF, "Unused / Mirror", true, nullptr},
        });
    add(57, "Fairchild Channel F");
    add(45, "Intellivision");
    add(74, "Interton VC 4000");
    add(23, "Magnavox Odyssey 2");
    add(69, "Mega Duck");
    add(29, "MSX");
    add(102, "Standalone");
    add(80, "Uzebox");
    add(46, "Vectrex");
    add(72, "WASM-4");
    add(63, "Watara Supervision");
    add(53, "WonderSwan");
    add(59, "ZX Spectrum");

    return map;
}

} // namespace

const ConsoleInfo* ConsoleRegions::GetConsoleInfo(int consoleId) {
    static const QMap<int, ConsoleInfo> consoleMap = BuildConsoleMap();
    auto it = consoleMap.constFind(consoleId);
    if (it != consoleMap.constEnd())
        return &it.value();
    return nullptr;
}

} // namespace RALE
