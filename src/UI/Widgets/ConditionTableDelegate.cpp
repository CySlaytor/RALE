#include "ConditionTableDelegate.h"
#include "../Models/ConditionTableModel.h"
#include "../../Definitions/RaLogicSyntax.h"
#include "../Styles/ThemeManager.h"
#include <QComboBox>
#include <QLineEdit>
#include <QRegularExpressionValidator>
#include <QPainter>

namespace RALE {

ConditionTableDelegate::ConditionTableDelegate(QObject *parent)
    : QStyledItemDelegate(parent)
{
}

void ConditionTableDelegate::paint(QPainter *painter, const QStyleOptionViewItem &option, const QModelIndex &index) const
{
    QStyleOptionViewItem opt = option;
    initStyleOption(&opt, index);

    bool isSelected = (opt.state & QStyle::State_Selected);
    bool isColId = (index.column() == ConditionTableModel::ColID);
    bool isDark = (opt.palette.window().color().red() < 128);

    // Determine cell background color
    QColor bgColor;
    bool hasCustomBg = false;

    QVariant bgVariant = index.data(Qt::BackgroundRole);
    if (bgVariant.isValid())
    {
        bgColor = bgVariant.value<QColor>();
        hasCustomBg = true;
    }

    // Explicit pure white in light mode, dark gray in dark mode
    QColor baseCellColor = isDark ? QColor(32, 32, 34) : QColor(255, 255, 255);

    // ColID retains its issue/status color, while cells 1..9 use row selection highlight
    if (isSelected && !isColId)
    {
        bgColor = opt.palette.highlight().color(); // Solid #0078D7 in light theme
    }
    else if (!hasCustomBg)
    {
        bgColor = baseCellColor;
    }

    // 1. Fill cell background
    painter->fillRect(opt.rect, bgColor);

    // 2. Draw clean single 1px thin gridline on right and bottom of cell matching DataGridView
    QColor gridColor = isDark ? QColor(46, 46, 50) : QColor(112, 112, 112);
    painter->save();
    painter->setPen(QPen(gridColor, 1));
    painter->drawLine(opt.rect.right(), opt.rect.top(), opt.rect.right(), opt.rect.bottom());
    painter->drawLine(opt.rect.left(), opt.rect.bottom(), opt.rect.right(), opt.rect.bottom());
    painter->restore();

    // 3. Determine text color
    QColor textColor;
    if (isSelected && !isColId)
    {
        textColor = opt.palette.highlightedText().color(); // White text on selection
    }
    else
    {
        QVariant fg = index.data(Qt::ForegroundRole);
        if (fg.isValid())
            textColor = fg.value<QColor>();
        else
            textColor = opt.palette.text().color();
    }

    // 4. Draw text with proper alignment and 4px padding
    painter->save();
    painter->setPen(textColor);
    painter->setFont(opt.font);

    int alignFlags = Qt::AlignVCenter | Qt::TextSingleLine;
    if (index.column() == ConditionTableModel::ColCmp || index.column() == ConditionTableModel::ColHits)
        alignFlags |= Qt::AlignCenter;
    else
        alignFlags |= Qt::AlignLeft;

    QRect textRect = opt.rect.adjusted(4, 0, -4, 0);
    painter->setClipRect(opt.rect.adjusted(1, 1, -1, -1));
    painter->drawText(textRect, alignFlags, opt.text);
    painter->restore();
}

QWidget *ConditionTableDelegate::createEditor(
    QWidget *parent,
    const QStyleOptionViewItem & /*option*/,
    const QModelIndex &index) const
{
    if (!index.isValid()) return nullptr;

    switch (index.column())
    {
        case ConditionTableModel::ColFlag:
        {
            auto *cb = new QComboBox(parent);
            cb->setFrame(false);
            for (const auto &flag : RaLogicSyntax::Flags)
                cb->addItem(flag);
            ThemeManager::SetupComboBox(cb, 10);
            return cb;
        }
        case ConditionTableModel::ColLType:
        case ConditionTableModel::ColRType:
        {
            auto *cb = new QComboBox(parent);
            cb->setFrame(false);
            for (const auto &t : RaLogicSyntax::OperandTypes)
                cb->addItem(t);
            ThemeManager::SetupComboBox(cb, 10);
            return cb;
        }
        case ConditionTableModel::ColLSize:
        case ConditionTableModel::ColRSize:
        {
            auto *cb = new QComboBox(parent);
            cb->setFrame(false);
            for (const auto &s : RaLogicSyntax::MemorySizes)
                cb->addItem(s);
            ThemeManager::SetupComboBox(cb, 10);
            return cb;
        }
        case ConditionTableModel::ColCmp:
        {
            auto *cb = new QComboBox(parent);
            cb->setFrame(false);
            const auto *model = qobject_cast<const ConditionTableModel*>(index.model());
            QString flag = "";
            if (model != nullptr)
            {
                auto flagIdx = model->index(index.row(), ConditionTableModel::ColFlag);
                flag = flagIdx.data(Qt::DisplayRole).toString();
            }

            if (RaLogicSyntax::ArithmeticFlags.contains(flag))
            {
                for (const auto &op : RaLogicSyntax::ArithmeticOperators)
                    cb->addItem(op);
            }
            else if (RaLogicSyntax::StrictComparisonFlags.contains(flag))
            {
                for (const auto &op : RaLogicSyntax::ComparisonOperators)
                {
                    if (op != RaLogicSyntax::NO_OPERATOR_TEXT)
                        cb->addItem(op);
                }
            }
            else
            {
                for (const auto &op : RaLogicSyntax::ComparisonOperators)
                    cb->addItem(op);
            }
            ThemeManager::SetupComboBox(cb, 8);
            return cb;
        }
        case ConditionTableModel::ColLValue:
        case ConditionTableModel::ColRValue:
        {
            auto *edit = new QLineEdit(parent);
            edit->setFrame(false);
            const auto *model = qobject_cast<const ConditionTableModel*>(index.model());
            int typeCol = (index.column() == ConditionTableModel::ColLValue)
                ? ConditionTableModel::ColLType
                : ConditionTableModel::ColRType;
            QString type = "";
            if (model != nullptr)
            {
                type = model->index(index.row(), typeCol).data(Qt::DisplayRole).toString();
            }

            if (type == "Float")
            {
                edit->setMaxLength(32);
                // Match C# keystroke filtering: hex chars plus float punctuation.
                static const QRegularExpression floatRx(R"([0-9a-fA-FxX.,+\-]*)");
                edit->setValidator(new QRegularExpressionValidator(floatRx, edit));
            }
            else if (type == "Value")
            {
                edit->setMaxLength(15);
                static const QRegularExpression valueRx(R"([0-9a-fA-FxX]*)");
                edit->setValidator(new QRegularExpressionValidator(valueRx, edit));
            }
            else
            {
                edit->setMaxLength(10);
                static const QRegularExpression memRx(R"([0-9a-fA-FxX]*)");
                edit->setValidator(new QRegularExpressionValidator(memRx, edit));
            }

            // C# lowercases the whole cell; keep the caret position while doing so.
            QObject::connect(edit, &QLineEdit::textEdited, edit, [edit](const QString &text) {
                QString lower = text.toLower();
                if (lower == text) return;
                int pos = edit->cursorPosition();
                edit->setText(lower);
                edit->setCursorPosition(pos);
            });
            return edit;
        }
        case ConditionTableModel::ColHits:
        {
            auto *edit = new QLineEdit(parent);
            edit->setFrame(false);
            edit->setAlignment(Qt::AlignCenter);
            static const QRegularExpression hitRx(R"(\d*)");
            edit->setValidator(new QRegularExpressionValidator(hitRx, edit));
            edit->setMaxLength(10);
            return edit;
        }
    }

    return nullptr;
}

void ConditionTableDelegate::setEditorData(QWidget *editor, const QModelIndex &index) const
{
    if (!index.isValid()) return;

    if (auto *cb = qobject_cast<QComboBox*>(editor))
    {
        QString val = index.data(Qt::EditRole).toString();
        if (index.column() == ConditionTableModel::ColFlag && (val.isEmpty() || val == RaLogicSyntax::NO_FLAG_TEXT))
            val = RaLogicSyntax::NO_FLAG_TEXT;
        else if (index.column() == ConditionTableModel::ColCmp && (val.isEmpty() || val == RaLogicSyntax::NO_OPERATOR_TEXT))
            val = RaLogicSyntax::NO_OPERATOR_TEXT;

        int idx = cb->findText(val);
        if (idx != -1) cb->setCurrentIndex(idx);
        else cb->setEditText(val);
    }
    else if (auto *edit = qobject_cast<QLineEdit*>(editor))
    {
        QString val = index.data(Qt::EditRole).toString();
        if (index.column() == ConditionTableModel::ColLValue || index.column() == ConditionTableModel::ColRValue)
        {
            const auto *model = qobject_cast<const ConditionTableModel*>(index.model());
            int typeCol = (index.column() == ConditionTableModel::ColLValue)
                ? ConditionTableModel::ColLType
                : ConditionTableModel::ColRType;
            QString type = "";
            if (model != nullptr)
            {
                type = model->index(index.row(), typeCol).data(Qt::DisplayRole).toString();
            }

            if (type != "Value" && type != "Float")
            {
                if (val.startsWith("0x", Qt::CaseInsensitive))
                    val = val.mid(2);
            }
        }
        edit->setText(val);
    }
}

void ConditionTableDelegate::setModelData(
    QWidget *editor,
    QAbstractItemModel *model,
    const QModelIndex &index) const
{
    if (!index.isValid() || model == nullptr) return;

    if (auto *cb = qobject_cast<QComboBox*>(editor))
    {
        QString text = cb->currentText();
        if (text == RaLogicSyntax::NO_FLAG_TEXT || text == RaLogicSyntax::NO_OPERATOR_TEXT)
            text = "";
        model->setData(index, text, Qt::EditRole);
    }
    else if (auto *edit = qobject_cast<QLineEdit*>(editor))
    {
        model->setData(index, edit->text(), Qt::EditRole);
    }
}

void ConditionTableDelegate::updateEditorGeometry(
    QWidget *editor,
    const QStyleOptionViewItem &option,
    const QModelIndex & /*index*/) const
{
    editor->setGeometry(option.rect.adjusted(1, 1, -1, -1));
}

} // namespace RALE
