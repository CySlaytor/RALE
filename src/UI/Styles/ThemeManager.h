#pragma once

#include <QString>
#include <QApplication>
#include <QProxyStyle>
#include <QStyledItemDelegate>
#include <QComboBox>
#include <QListView>
#include <QEvent>
#include <QPropertyAnimation>
#include <QEasingCurve>
#include <QWidget>

namespace RALogicEditor {

// Forces classic Win32 dropdown positioning below the QComboBox across the entire app
class ClassicDropdownStyle : public QProxyStyle {
public:
    explicit ClassicDropdownStyle(QStyle* baseStyle = nullptr) : QProxyStyle(baseStyle) {}

    int styleHint(StyleHint hint, const QStyleOption* option = nullptr,
                  const QWidget* widget = nullptr,
                  QStyleHintReturn* returnData = nullptr) const override {
        if (hint == QStyle::SH_ComboBox_Popup) {
            return 0; // 0 = false: drop down directly below the combobox
        }
        return QProxyStyle::styleHint(hint, option, widget, returnData);
    }
};

class GlobalComboBoxConfigurator : public QObject {
public:
    explicit GlobalComboBoxConfigurator(QObject* parent = nullptr) : QObject(parent) {}

    static void configureComboBox(QComboBox* combo) {
        if (!combo || combo->property("raDropdownConfigured").toBool()) {
            return;
        }
        combo->setProperty("raDropdownConfigured", true);

        combo->setFixedHeight(26);
        combo->setMaxVisibleItems(14);

        auto* popupView = new QListView(combo);
        popupView->setObjectName("comboPopupView");
        popupView->setUniformItemSizes(true);
        popupView->setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        popupView->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);

        combo->setView(popupView);
    }

protected:
    bool eventFilter(QObject* obj, QEvent* event) override {
        if (event->type() == QEvent::Polish) {
            if (auto* combo = qobject_cast<QComboBox*>(obj)) {
                configureComboBox(combo);
            }
        }
        return QObject::eventFilter(obj, event);
    }
};

class TooltipAnimationFilter : public QObject {
    Q_OBJECT
  public:
    explicit TooltipAnimationFilter(QObject* parent = nullptr) : QObject(parent) {}

  protected:
    bool eventFilter(QObject* obj, QEvent* event) override {
#ifdef Q_OS_WIN
        if (obj && obj->isWidgetType()) {
            auto* w = static_cast<QWidget*>(obj);
            if (w->inherits("QTipLabel")) {
                if (event->type() == QEvent::Show) {
                    if (auto* existingAnim = w->findChild<QPropertyAnimation*>("tooltip_anim")) {
                        existingAnim->stop();
                        delete existingAnim;
                    }

                    w->setProperty("pf_closing", false);
                    w->setProperty("pf_allow_close", false);
                    w->setWindowOpacity(0.0);

                    auto* anim = new QPropertyAnimation(w, "windowOpacity", w);
                    anim->setObjectName("tooltip_anim");
                    anim->setDuration(120);
                    anim->setStartValue(0.0);
                    anim->setEndValue(1.0);
                    anim->setEasingCurve(QEasingCurve::OutQuad);
                    anim->start(QAbstractAnimation::DeleteWhenStopped);
                } else if (event->type() == QEvent::Close) {
                    if (w->property("pf_allow_close").toBool()) {
                        return false;
                    }

                    event->ignore();

                    if (!w->property("pf_closing").toBool()) {
                        w->setProperty("pf_closing", true);

                        if (auto* existingAnim = w->findChild<QPropertyAnimation*>("tooltip_anim")) {
                            existingAnim->stop();
                            delete existingAnim;
                        }

                        if (w->windowOpacity() <= 0.05) {
                            w->setProperty("pf_allow_close", true);
                            w->close();
                            w->deleteLater();
                            return true;
                        }

                        auto* anim = new QPropertyAnimation(w, "windowOpacity", w);
                        anim->setObjectName("tooltip_anim");
                        anim->setDuration(140);
                        anim->setStartValue(w->windowOpacity());
                        anim->setEndValue(0.0);
                        anim->setEasingCurve(QEasingCurve::OutQuad);
                        connect(anim, &QPropertyAnimation::finished, w, [w]() {
                            w->setProperty("pf_allow_close", true);
                            w->close();
                            w->deleteLater();
                        });
                        anim->start(QAbstractAnimation::DeleteWhenStopped);
                    }
                    return true;
                }
            }
        }
#endif
        return QObject::eventFilter(obj, event);
    }
};

// Custom proxy style ensuring vector checkboxes and clean combobox arrows across all platforms
class RaStyle : public ClassicDropdownStyle
{
public:
    using ClassicDropdownStyle::ClassicDropdownStyle;

    void drawPrimitive(PrimitiveElement element, const QStyleOption *option, QPainter *painter, const QWidget *widget = nullptr) const override;
    QRect subElementRect(SubElement element, const QStyleOption *option, const QWidget *widget = nullptr) const override;
    int styleHint(StyleHint hint, const QStyleOption *option = nullptr, const QWidget *widget = nullptr, QStyleHintReturn *returnData = nullptr) const override;
};

// Delegate ensuring 1:1 Windows Forms blue highlight (#0078D7) for current/hovered dropdown items
class RaComboBoxDelegate : public QStyledItemDelegate
{
    Q_OBJECT
public:
    explicit RaComboBoxDelegate(QObject *parent = nullptr);

    void paint(QPainter *painter, const QStyleOptionViewItem &option, const QModelIndex &index) const override;
    QSize sizeHint(const QStyleOptionViewItem &option, const QModelIndex &index) const override;
};

class ThemeManager
{
public:
    enum class Theme {
        Dark,
        Light
    };

    static void ApplyTheme(Theme theme);
    static Theme CurrentTheme();

    // Configures a QComboBox with uniform item sizing, max visible items (default 8), and the Win32 selection delegate
    static void SetupComboBox(QComboBox *cb, int maxVisibleItems = 14);

private:
    static Theme _currentTheme;
    static QString LoadStyleSheet(const QString &resourcePath);

    static void ApplyLightPalette(QApplication *app);
    static void ApplyDarkPalette(QApplication *app);
};

} // namespace RALogicEditor
