#include "ThemeManager.h"

#include <QAbstractItemView>
#include <QCursor>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QListView>
#include <QPainter>
#include <QPainterPath>
#include <QPalette>
#include <QStyleFactory>
#include <QStyleOptionButton>
#include <QStyleOptionViewItem>
#include <QTextStream>
#include <QUrl>
#include <QtSvg/QSvgRenderer>

namespace RALE {

ThemeManager::Theme ThemeManager::_currentTheme = ThemeManager::Theme::Light;
QHash<QString, QIcon> ThemeManager::_iconCache;

// Event filter to prevent scrollbar layout thrashing during opening animation
class ComboBoxScrollbarFilter : public QObject {
  public:
    explicit ComboBoxScrollbarFilter(QComboBox* cb) : QObject(cb), _cb(cb) {}

  protected:
    bool eventFilter(QObject* obj, QEvent* event) override {
        if (event->type() == QEvent::MouseButtonPress || event->type() == QEvent::KeyPress) {
            if (_cb != nullptr && _cb->view() != nullptr) {
                bool needsScroll = _cb->count() > _cb->maxVisibleItems();
                _cb->view()->setVerticalScrollBarPolicy(needsScroll ? Qt::ScrollBarAsNeeded : Qt::ScrollBarAlwaysOff);
            }
        }
        return QObject::eventFilter(obj, event);
    }

  private:
    QComboBox* _cb = nullptr;
};

RaComboBoxDelegate::RaComboBoxDelegate(QObject* parent) : QStyledItemDelegate(parent) {}

void RaComboBoxDelegate::paint(QPainter* painter, const QStyleOptionViewItem& option, const QModelIndex& index) const {
    QStyleOptionViewItem opt = option;
    initStyleOption(&opt, index);

    // Suppress dashed focus rect
    opt.state &= ~QStyle::State_HasFocus;

    bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);

    // Determine single active highlight cleanly without multi-item conflict
    bool isHighlighted = false;
    const auto* view = qobject_cast<const QAbstractItemView*>(opt.widget);
    if (view != nullptr && view->viewport() != nullptr) {
        QPoint pos = view->viewport()->mapFromGlobal(QCursor::pos());
        if (view->viewport()->rect().contains(pos)) {
            QModelIndex hoveredIndex = view->indexAt(pos);
            int activeRow = hoveredIndex.isValid() ? hoveredIndex.row() : view->currentIndex().row();
            isHighlighted = (index.row() == activeRow);
        } else {
            isHighlighted = (index.row() == view->currentIndex().row());
        }
    } else {
        isHighlighted = ((opt.state & QStyle::State_Selected) != 0);
    }

    painter->save();
    if (isHighlighted) {
        // Windows Forms highlight blue #0078D7 (#007acc in dark mode)
        QColor selBg = isDark ? QColor(0, 120, 215) : QColor(0, 120, 215);
        painter->fillRect(opt.rect, selBg);
        painter->setPen(Qt::white);
    } else {
        QColor normBg = isDark ? QColor(32, 32, 34) : QColor(255, 255, 255);
        QColor normFg = isDark ? QColor(240, 240, 240) : QColor(0, 0, 0);
        painter->fillRect(opt.rect, normBg);
        painter->setPen(normFg);
    }

    painter->setFont(opt.font);
    QRect textRect = opt.rect.adjusted(3, 0, -3, 0);
    painter->drawText(textRect, Qt::AlignLeft | Qt::AlignVCenter, opt.text);
    painter->restore();
}

QSize RaComboBoxDelegate::sizeHint(const QStyleOptionViewItem& option, const QModelIndex& index) const {
    QSize s = QStyledItemDelegate::sizeHint(option, index);
    s.setHeight(17); // 17px per item matches Windows Forms combobox dropdown line height
    return s;
}

void ThemeManager::SetupComboBox(QComboBox* cb, int maxVisibleItems) {
    if (cb == nullptr)
        return;
    GlobalComboBoxConfigurator::configureComboBox(cb);
    if (maxVisibleItems > 0)
        cb->setMaxVisibleItems(maxVisibleItems);
    else
        cb->setMaxVisibleItems(14);
}

void ThemeManager::clearCache() { _iconCache.clear(); }

QIcon ThemeManager::getIcon(const QString& resourcePath, Theme theme) {
    QString themeName = (theme == Theme::Dark) ? "Dark" : "Light";
    QString cacheKey = QString("%1_%2").arg(resourcePath, themeName);
    if (_iconCache.contains(cacheKey)) {
        return _iconCache.value(cacheKey);
    }

    QFile file(resourcePath);
    if (!file.open(QFile::ReadOnly)) {
        return QIcon(resourcePath);
    }
    QString svgContent = QString::fromUtf8(file.readAll());
    file.close();

    QString primaryColor;
    QString accentColor;

    if (theme == Theme::Light) {
        if (resourcePath.contains("find.svg", Qt::CaseInsensitive)) {
            primaryColor = "#666666";
        } else {
            primaryColor = "#3a3a3a";
        }
        accentColor = "#0056b3";
    } else {
        primaryColor = "#e0e0e0";
        accentColor = "#7289da";
    }

    svgContent.replace("#e0e0e0", primaryColor, Qt::CaseInsensitive);
    svgContent.replace("#7289da", accentColor, Qt::CaseInsensitive);

    QByteArray svgData = svgContent.toUtf8();
    QIcon icon;

    const QList<int> standardSizes = {16, 20, 24, 32, 48, 64, 128, 256};
    for (int size : standardSizes) {
        QSvgRenderer renderer(svgData);
        if (renderer.isValid()) {
            QPixmap pixmap(size, size);
            pixmap.fill(Qt::transparent);
            QPainter painter(&pixmap);
            painter.setRenderHint(QPainter::Antialiasing);
            painter.setRenderHint(QPainter::SmoothPixmapTransform);
            renderer.render(&painter);
            painter.end();
            icon.addPixmap(pixmap);
        }
    }

    if (icon.isNull()) {
        return QIcon(resourcePath);
    }

    _iconCache.insert(cacheKey, icon);
    return icon;
}

QRect RaStyle::subElementRect(SubElement element, const QStyleOption* option, const QWidget* widget) const {
    // Enforce strict 13x13 indicator geometry across all item views (TreeWidget, ListWidget, TableWidget)
    if (element == SE_ItemViewItemCheckIndicator || element == SE_CheckBoxIndicator) {
        QRect r = QProxyStyle::subElementRect(element, option, widget);
        int size = 13;
        int x = r.x() + (r.width() - size) / 2;
        int y = r.y() + (r.height() - size) / 2;
        return QRect(x, y, size, size);
    }
    return QProxyStyle::subElementRect(element, option, widget);
}

void RaStyle::drawPrimitive(PrimitiveElement element, const QStyleOption* option, QPainter* painter, const QWidget* widget) const {
    if (element == PE_IndicatorCheckBox || element == PE_IndicatorItemViewItemCheck) {
        // Support all checkbox indicators uniformly matching Screenshot 2 exactly
        if (option != nullptr) {
            painter->save();
            painter->setRenderHint(QPainter::Antialiasing, true);

            QRect r = option->rect;
            int size = 13;
            // Center the 13x13 box within the indicator rect
            qreal x = r.x() + (r.width() - size) / 2.0;
            qreal y = r.y() + (r.height() - size) / 2.0;
            // Inset by 0.5px so the 1px pen aligns precisely to the device pixel grid without edge clipping
            QRectF boxRect(x + 0.5, y + 0.5, size - 1.0, size - 1.0);

            // Correctly determine checked status whether delivered via QStyleOptionViewItem or QStyleOptionButton
            bool isChecked = false;
            if (const auto* viewOpt = qstyleoption_cast<const QStyleOptionViewItem*>(option)) {
                isChecked = (viewOpt->checkState == Qt::Checked) || ((viewOpt->state & State_On) != 0);
            } else {
                isChecked = ((option->state & State_On) != 0);
            }

            bool isEnabled = ((option->state & State_Enabled) != 0);
            bool isHover = isEnabled && ((option->state & State_MouseOver) != 0);
            // Query ThemeManager directly to ensure light theme always renders clean white regardless of parent palette overrides
            bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);

            if (isChecked) {
                // Solid blue box with crisp white checkmark matching Screenshot 6
                QColor bg = !isEnabled ? (isDark ? QColor(60, 60, 65) : QColor(180, 180, 185))
                                       : (isHover ? QColor(16, 110, 190) : QColor(0, 120, 215));
                painter->setBrush(bg);
                painter->setPen(bg);
                painter->drawRoundedRect(boxRect, 2, 2);

                QPen checkPen(Qt::white, 1.8);
                checkPen.setCapStyle(Qt::RoundCap);
                checkPen.setJoinStyle(Qt::RoundJoin);
                painter->setPen(checkPen);

                QPainterPath path;
                path.moveTo(x + 2.5, y + 6.0);
                path.lineTo(x + 5.0, y + 9.5);
                path.lineTo(x + 10.5, y + 3.0);
                painter->drawPath(path);
            } else {
                // Unchecked: clean white box with 1px border
                QColor bg = isDark ? QColor(32, 32, 34) : (isHover ? QColor(248, 248, 248) : QColor(255, 255, 255));
                QColor border = !isEnabled ? (isDark ? QColor(60, 60, 65) : QColor(190, 190, 190))
                                           : (isHover ? (isDark ? QColor(0, 120, 215) : QColor(0, 120, 215))
                                                      : (isDark ? QColor(63, 63, 70) : QColor(112, 112, 112)));
                painter->setBrush(bg);
                painter->setPen(border);
                painter->drawRoundedRect(boxRect, 2, 2);
            }

            painter->restore();
            return;
        }
    } else if (element == PE_IndicatorBranch) {
        // 1:1 reproduction of Windows Explorer / WinForms TreeView classic dotted lines and [+] / [-] boxes
        if (option != nullptr) {
            painter->save();
            painter->setRenderHint(QPainter::Antialiasing, false);

            QRect r = option->rect;
            int midX = r.left() + r.width() / 2;
            int midY = r.top() + r.height() / 2;

            bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
            QColor lineColor = isDark ? QColor(80, 80, 85) : QColor(160, 160, 160);
            QPen dotPen(lineColor, 1, Qt::DotLine);
            painter->setPen(dotPen);

            bool hasSiblings = (option->state & State_Sibling);
            bool hasChildren = (option->state & State_Children);
            bool isOpen = (option->state & State_Open);
            bool connectsToItem = (option->state & State_Item);

            // Vertical connecting line
            if (hasSiblings) {
                painter->drawLine(midX, r.top(), midX, r.bottom());
            } else if (connectsToItem || hasChildren) {
                painter->drawLine(midX, r.top(), midX, midY);
            }

            // Horizontal branch line connecting to item
            if (connectsToItem || hasChildren) {
                painter->drawLine(midX, midY, r.right(), midY);
            }

            // Classic [+] / [-] 9x9 box button for expandable nodes
            if (hasChildren) {
                QRect boxRect(midX - 4, midY - 4, 8, 8);
                QColor boxBg = isDark ? QColor(36, 36, 40) : QColor(255, 255, 255);
                QColor boxBorder = isDark ? QColor(140, 140, 145) : QColor(112, 112, 112);

                painter->fillRect(boxRect, boxBg);
                painter->setPen(boxBorder);
                painter->drawRect(boxRect);

                // Minus line (horizontal)
                painter->setPen(isDark ? QColor(240, 240, 240) : QColor(20, 20, 20));
                painter->drawLine(midX - 2, midY, midX + 2, midY);

                // Plus line (vertical) if collapsed
                if (!isOpen) {
                    painter->drawLine(midX, midY - 2, midX, midY + 2);
                }
            }

            painter->restore();
            return;
        }
    }

    QProxyStyle::drawPrimitive(element, option, painter, widget);
}

int RaStyle::styleHint(StyleHint hint, const QStyleOption* option, const QWidget* widget, QStyleHintReturn* returnData) const {
    // Always Windows drop-down list anchored below combobox, never popup menu centered over combobox
    if (hint == SH_ComboBox_Popup)
        return 0;
    return ClassicDropdownStyle::styleHint(hint, option, widget, returnData);
}

void ThemeManager::ApplyTheme(Theme theme) {
    _currentTheme = theme;
    clearCache();

    QApplication::setEffectEnabled(Qt::UI_AnimateTooltip, false);
    QApplication::setEffectEnabled(Qt::UI_FadeTooltip, false);

    // Apply RaStyle over Fusion base engine
    qApp->setStyle(new RaStyle(QStyleFactory::create("Fusion")));

    if (theme == Theme::Dark) {
        ApplyDarkPalette(qApp);
        QString qss = LoadStyleSheet(":/styles/dark_theme.qss");
        qApp->setStyleSheet(qss);
    } else {
        ApplyLightPalette(qApp);
        QString qss = LoadStyleSheet(":/styles/light_theme.qss");
        qApp->setStyleSheet(qss);
    }
}

ThemeManager::Theme ThemeManager::CurrentTheme() { return _currentTheme; }

void ThemeManager::ApplyDarkPalette(QApplication* app) {
    QPalette darkPalette;
    darkPalette.setColor(QPalette::Window, QColor(32, 32, 32));
    darkPalette.setColor(QPalette::WindowText, Qt::white);
    darkPalette.setColor(QPalette::Base, QColor(25, 25, 25));
    darkPalette.setColor(QPalette::AlternateBase, QColor(32, 32, 32));
    darkPalette.setColor(QPalette::ToolTipBase, QColor(36, 36, 36));
    darkPalette.setColor(QPalette::ToolTipText, QColor(240, 240, 240));
    darkPalette.setColor(QPalette::Text, Qt::white);
    darkPalette.setColor(QPalette::Button, QColor(55, 55, 55));
    darkPalette.setColor(QPalette::ButtonText, Qt::white);
    darkPalette.setColor(QPalette::BrightText, Qt::red);
    darkPalette.setColor(QPalette::Link, QColor(3, 218, 198));
    darkPalette.setColor(QPalette::Highlight, QColor(58, 120, 203));
    darkPalette.setColor(QPalette::HighlightedText, Qt::white);
    darkPalette.setColor(QPalette::Disabled, QPalette::WindowText, QColor(128, 128, 128));
    darkPalette.setColor(QPalette::Disabled, QPalette::Text, QColor(128, 128, 128));
    darkPalette.setColor(QPalette::Disabled, QPalette::ButtonText, QColor(128, 128, 128));
    app->setPalette(darkPalette);
}

void ThemeManager::ApplyLightPalette(QApplication* app) {
    QPalette lightPalette;
    lightPalette.setColor(QPalette::Window, QColor(240, 240, 240));
    lightPalette.setColor(QPalette::WindowText, QColor(0, 0, 0));
    lightPalette.setColor(QPalette::Base, QColor(255, 255, 255));
    lightPalette.setColor(QPalette::AlternateBase, QColor(248, 249, 250));
    lightPalette.setColor(QPalette::ToolTipBase, QColor(248, 248, 248));
    lightPalette.setColor(QPalette::ToolTipText, QColor(32, 32, 32));
    lightPalette.setColor(QPalette::Text, QColor(0, 0, 0));
    lightPalette.setColor(QPalette::Button, QColor(240, 240, 240));
    lightPalette.setColor(QPalette::ButtonText, QColor(0, 0, 0));
    lightPalette.setColor(QPalette::BrightText, Qt::red);
    lightPalette.setColor(QPalette::Link, QColor(0, 102, 204));
    lightPalette.setColor(QPalette::Highlight, QColor(58, 120, 203));
    lightPalette.setColor(QPalette::HighlightedText, Qt::white);
    lightPalette.setColor(QPalette::Disabled, QPalette::WindowText, QColor(138, 138, 138));
    lightPalette.setColor(QPalette::Disabled, QPalette::Text, QColor(138, 138, 138));
    lightPalette.setColor(QPalette::Disabled, QPalette::ButtonText, QColor(138, 138, 138));
    app->setPalette(lightPalette);
}

QString ThemeManager::LoadStyleSheet(const QString& resourcePath) {
    // Search physical disk candidates first so stylesheet edits take effect immediately
    QString fileName = QFileInfo(resourcePath).fileName();
    QString appDir = QCoreApplication::applicationDirPath();
    QStringList candidates = {QDir(appDir).filePath("src/UI/Styles/" + fileName),
                              QDir(appDir).filePath("../src/UI/Styles/" + fileName),
                              QDir(appDir).filePath("../../src/UI/Styles/" + fileName),
                              QDir(appDir).filePath("../../../src/UI/Styles/" + fileName),
                              QDir(appDir).filePath("styles/" + fileName),
                              QDir(appDir).filePath(fileName)};

    for (const auto& candidate : candidates) {
        QFile diskFile(candidate);
        if (diskFile.open(QIODevice::ReadOnly | QIODevice::Text)) {
            QTextStream in(&diskFile);
            return in.readAll();
        }
    }

    // Fallback: compiled Qt resource
    QFile file(resourcePath);
    if (file.open(QIODevice::ReadOnly | QIODevice::Text)) {
        QTextStream in(&file);
        return in.readAll();
    }

    return "";
}

} // namespace RALE
