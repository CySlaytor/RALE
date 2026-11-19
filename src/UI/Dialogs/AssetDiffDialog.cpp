#include "AssetDiffDialog.h"
#include "../../Common/AppSettings.h"
#include "../../Parsers/AchievementParser.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QTextCursor>
#include <QTextCharFormat>
#include <QScrollBar>
#include <algorithm>

namespace RALE {

AssetDiffDialog::AssetDiffDialog(
    const std::shared_ptr<IAssetItem> &serverItem,
    const std::shared_ptr<IAssetItem> &localItem,
    QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();
    loadPositionSettings();

    this->setWindowTitle(QString("Diff Viewer - %1 (ID: %2)").arg(localItem->Title).arg(localItem->ID));

    auto serverLines = GetReadableLines(serverItem != nullptr ? serverItem->Logic : "", serverItem != nullptr && serverItem->isLeaderboard());
    auto localLines = GetReadableLines(localItem != nullptr ? localItem->Logic : "", localItem != nullptr && localItem->isLeaderboard());

    AlignDiffLists(serverLines, localLines);
    HighlightDifferences(serverLines, localLines);
    UpdateDiffStats(serverLines, localLines);
}

void AssetDiffDialog::loadPositionSettings()
{
    QPoint p;
    QSize s;
    if (AppSettings::LoadDialogGeometry("AssetDiffDialog", p, s))
    {
        move(p);
        if (s.isValid())
        {
            resize(std::max(s.width(), 500), std::max(s.height(), 300));
        }
    }
}

void AssetDiffDialog::savePositionSettings()
{
    AppSettings::SaveDialogGeometry("AssetDiffDialog", pos(), size());
}

void AssetDiffDialog::done(int r)
{
    savePositionSettings();
    QDialog::done(r);
}

void AssetDiffDialog::InitializeUI()
{
    resize(900, 500);

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(5, 5, 5, 5);

    auto *splitDiff = new QSplitter(Qt::Horizontal, this);

    // Panel 1: Server
    auto *panel1 = new QWidget(splitDiff);
    auto *layout1 = new QVBoxLayout(panel1);
    layout1->setContentsMargins(5, 5, 5, 5);
    lblServer = new QLabel("Server Data", panel1);
    lblServer->setStyleSheet("font-weight: bold;");
    rtbServer = new QTextEdit(panel1);
    rtbServer->setFont(QFont("Consolas", 10));
    rtbServer->setReadOnly(true);
    rtbServer->setLineWrapMode(QTextEdit::NoWrap);
    layout1->addWidget(lblServer);
    layout1->addWidget(rtbServer);
    splitDiff->addWidget(panel1);

    // Panel 2: Local
    auto *panel2 = new QWidget(splitDiff);
    auto *layout2 = new QVBoxLayout(panel2);
    layout2->setContentsMargins(5, 5, 5, 5);
    lblLocal = new QLabel("Local Data", panel2);
    lblLocal->setStyleSheet("font-weight: bold;");
    rtbLocal = new QTextEdit(panel2);
    rtbLocal->setFont(QFont("Consolas", 10));
    rtbLocal->setReadOnly(true);
    rtbLocal->setLineWrapMode(QTextEdit::NoWrap);
    layout2->addWidget(lblLocal);
    layout2->addWidget(rtbLocal);
    splitDiff->addWidget(panel2);

    splitDiff->setStretchFactor(0, 1);
    splitDiff->setStretchFactor(1, 1);
    mainLayout->addWidget(splitDiff, 1);

    // Synchronize scrolling between Server and Local panes
    connect(rtbServer->verticalScrollBar(), &QScrollBar::valueChanged,
            rtbLocal->verticalScrollBar(), &QScrollBar::setValue);
    connect(rtbLocal->verticalScrollBar(), &QScrollBar::valueChanged,
            rtbServer->verticalScrollBar(), &QScrollBar::setValue);
    connect(rtbServer->horizontalScrollBar(), &QScrollBar::valueChanged,
            rtbLocal->horizontalScrollBar(), &QScrollBar::setValue);
    connect(rtbLocal->horizontalScrollBar(), &QScrollBar::valueChanged,
            rtbServer->horizontalScrollBar(), &QScrollBar::setValue);

    lblStats = new QLabel("Changes: +0 / -0", this);
    lblStats->setStyleSheet("color: gray;");
    mainLayout->addWidget(lblStats);
}

QStringList AssetDiffDialog::GetReadableLines(const QString &rawLogic, bool isLeaderboard)
{
    QStringList lines;
    if (rawLogic.isEmpty())
    {
        lines.append("(Empty)");
        return lines;
    }

    auto groups = AchievementParser::ParseAchievementTrigger(rawLogic, 'S', isLeaderboard);

    for (const auto &group : groups)
    {
        lines.append(QString("[%1]").arg(group.GroupName));

        for (const auto &cond : group.Conditions)
        {
            lines.append(ConditionToAlignedString(cond));
        }

        lines.append("");
    }

    return lines;
}

QString AssetDiffDialog::ConditionToAlignedString(const AchievementCondition &cond)
{
    QString sb;

    QString flag = cond.Flag.isEmpty() ? "" : cond.Flag + ":";
    sb += flag.leftJustified(14, ' ');

    sb += OperandToAligned(cond.LeftOperand);

    QString op = cond.Operator.isEmpty() ? "" : cond.Operator;
    sb += "  " + op.leftJustified(3, ' ');

    if (!cond.Operator.isEmpty())
    {
        sb += " " + OperandToAligned(cond.RightOperand);
    }
    else
    {
        sb += QString(28, ' ');
    }

    if (cond.RequiredHits > 0)
    {
        sb += QString(" (%1)").arg(cond.RequiredHits);
    }

    return sb;
}

QString AssetDiffDialog::OperandToAligned(const Operand &op)
{
    if (op.Type == "Recall") return QString("{Result}").leftJustified(27, ' ');

    QString type = op.Type.leftJustified(6, ' ');

    QString size = op.Size.isEmpty() ? "" : op.Size;
    size = size.leftJustified(9, ' ');

    QString val = op.Value;
    if (val.length() > 12) val = val.left(10) + "..";
    val = val.leftJustified(12, ' ');

    return QString("%1 %2 %3").arg(type, size, val);
}

void AssetDiffDialog::AlignDiffLists(QStringList &server, QStringList &local)
{
    int i = 0;
    int j = 0;
    QStringList newServer;
    QStringList newLocal;

    while (i < server.size() || j < local.size())
    {
        QString sLine = (i < server.size()) ? server[i] : QString();
        QString lLine = (j < local.size()) ? local[j] : QString();
        bool hasS = (i < server.size());
        bool hasL = (j < local.size());

        if (hasS && hasL && sLine == lLine)
        {
            newServer.append(sLine);
            newLocal.append(lLine);
            i++; j++;
        }
        else
        {
            int idxInLocal = -1;
            if (hasS)
            {
                for (int k = j; k < local.size(); ++k)
                {
                    if (local[k] == sLine) { idxInLocal = k; break; }
                }
            }

            int idxInServer = -1;
            if (hasL && i < server.size())
            {
                for (int k = i; k < server.size(); ++k)
                {
                    if (server[k] == lLine) { idxInServer = k; break; }
                }
            }

            if (idxInLocal != -1 && (idxInServer == -1 || idxInLocal - j < idxInServer - i))
            {
                while (j < idxInLocal)
                {
                    newServer.append("");
                    newLocal.append(local[j]);
                    j++;
                }
            }
            else if (idxInServer != -1)
            {
                while (i < idxInServer)
                {
                    newServer.append(server[i]);
                    newLocal.append("");
                    i++;
                }
            }
            else
            {
                newServer.append(hasS ? sLine : "");
                newLocal.append(hasL ? lLine : "");
                if (hasS) i++;
                if (hasL) j++;
            }
        }
    }
    server = newServer;
    local = newLocal;
}

void AssetDiffDialog::HighlightDifferences(const QStringList &serverLines, const QStringList &localLines)
{
    QColor addedBack(200, 255, 200);
    QColor removedBack(255, 200, 200);
    QColor metaColor = Qt::gray;

    if (palette().window().color().red() < 128)
    {
        addedBack = QColor(0, 50, 0);
        removedBack = QColor(50, 0, 0);
    }

    RenderBox(rtbServer, serverLines, localLines, removedBack, metaColor, true);
    RenderBox(rtbLocal, localLines, serverLines, addedBack, metaColor, false);
}

void AssetDiffDialog::RenderBox(
    QTextEdit *box,
    const QStringList &sourceLines,
    const QStringList &compareLines,
    const QColor &diffBack,
    const QColor &metaColor,
    bool /*isServer*/)
{
    box->clear();
    QTextCursor cursor(box->textCursor());

    for (int i = 0; i < sourceLines.size(); i++)
    {
        QString line = sourceLines[i];
        QString compareLine = (i < compareLines.size()) ? compareLines[i] : "";

        bool isEmpty = line.isEmpty();
        bool isMeta = line.startsWith('[');
        bool isSame = (line == compareLine);

        QTextCharFormat fmt;

        if (isMeta)
        {
            fmt.setForeground(metaColor);
        }
        else if (isEmpty)
        {
        }
        else if (!isSame)
        {
            fmt.setBackground(diffBack);
        }

        cursor.setCharFormat(fmt);
        cursor.insertText(line + "\n");

        QTextCharFormat resetFmt;
        cursor.setCharFormat(resetFmt);
    }
}

void AssetDiffDialog::UpdateDiffStats(const QStringList &serverLines, const QStringList &localLines)
{
    int added = 0;
    int removed = 0;

    int maxCount = std::max(serverLines.size(), localLines.size());
    for (int i = 0; i < maxCount; i++)
    {
        QString s = (i < serverLines.size()) ? serverLines[i] : "";
        QString l = (i < localLines.size()) ? localLines[i] : "";

        if (s != l)
        {
            if (!l.isEmpty() && s.isEmpty()) added++;
            if (!s.isEmpty() && l.isEmpty()) removed++;
            if (!s.isEmpty() && !l.isEmpty()) { added++; removed++; }
        }
    }

    lblStats->setText(QString("Changes: +%1 / -%2 lines").arg(added).arg(removed));
}

} // namespace RALE
