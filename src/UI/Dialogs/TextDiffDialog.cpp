#include "TextDiffDialog.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QSplitter>
#include <QRegularExpression>
#include <QScrollBar>
#include <algorithm>

namespace RALogicEditor {

TextDiffDialog::TextDiffDialog(
    const QString &title,
    const QString &leftText,
    const QString &rightText,
    QWidget *parent)
    : QDialog(parent)
{
    InitializeUI();
    this->setWindowTitle(title);
    ApplyTheme();

    // Normalize split to ensure clean lists
    QString normLeft = leftText;
    normLeft.replace("\r\n", "\n").replace('\r', '\n');
    QString normRight = rightText;
    normRight.replace("\r\n", "\n").replace('\r', '\n');

    QStringList leftLines = normLeft.split('\n');
    QStringList rightLines = normRight.split('\n');

    AlignDiffLists(leftLines, rightLines);
    HighlightDifferences(leftLines, rightLines);
    UpdateDiffStats(leftLines, rightLines);
}

void TextDiffDialog::InitializeUI()
{
    resize(900, 500);

    auto *mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(5, 5, 5, 5);

    auto *splitDiff = new QSplitter(Qt::Horizontal, this);

    // Panel 1: Left
    auto *panel1 = new QWidget(splitDiff);
    auto *layout1 = new QVBoxLayout(panel1);
    layout1->setContentsMargins(5, 5, 5, 5);
    lblLeft = new QLabel("Original (File)", panel1);
    lblLeft->setStyleSheet("font-weight: bold;");
    rtbLeft = new QTextEdit(panel1);
    rtbLeft->setFont(QFont("Consolas", 10));
    rtbLeft->setReadOnly(true);
    rtbLeft->setLineWrapMode(QTextEdit::NoWrap);
    layout1->addWidget(lblLeft);
    layout1->addWidget(rtbLeft);
    splitDiff->addWidget(panel1);

    // Panel 2: Right
    auto *panel2 = new QWidget(splitDiff);
    auto *layout2 = new QVBoxLayout(panel2);
    layout2->setContentsMargins(5, 5, 5, 5);
    lblRight = new QLabel("Current (Memory)", panel2);
    lblRight->setStyleSheet("font-weight: bold;");
    rtbRight = new QTextEdit(panel2);
    rtbRight->setFont(QFont("Consolas", 10));
    rtbRight->setReadOnly(true);
    rtbRight->setLineWrapMode(QTextEdit::NoWrap);
    layout2->addWidget(lblRight);
    layout2->addWidget(rtbRight);
    splitDiff->addWidget(panel2);

    splitDiff->setStretchFactor(0, 1);
    splitDiff->setStretchFactor(1, 1);
    mainLayout->addWidget(splitDiff, 1);

    // Synchronize scrolling between Left and Right panes
    connect(rtbLeft->verticalScrollBar(), &QScrollBar::valueChanged,
            rtbRight->verticalScrollBar(), &QScrollBar::setValue);
    connect(rtbRight->verticalScrollBar(), &QScrollBar::valueChanged,
            rtbLeft->verticalScrollBar(), &QScrollBar::setValue);
    connect(rtbLeft->horizontalScrollBar(), &QScrollBar::valueChanged,
            rtbRight->horizontalScrollBar(), &QScrollBar::setValue);
    connect(rtbRight->horizontalScrollBar(), &QScrollBar::valueChanged,
            rtbLeft->horizontalScrollBar(), &QScrollBar::setValue);

    lblStats = new QLabel("Changes: +0 / -0", this);
    mainLayout->addWidget(lblStats);
}

void TextDiffDialog::ApplyTheme()
{
    if (palette().window().color().red() < 128)
    {
        QColor backColor(30, 30, 30);
        QColor foreColor(245, 245, 245);
        rtbLeft->setStyleSheet(QString("background-color: %1; color: %2; border: none;").arg(backColor.name(), foreColor.name()));
        rtbRight->setStyleSheet(QString("background-color: %1; color: %2; border: none;").arg(backColor.name(), foreColor.name()));
        lblStats->setStyleSheet("color: gray;");
    }
}

void TextDiffDialog::AlignDiffLists(QStringList &left, QStringList &right)
{
    // Simple greedy alignment algorithm
    // If line A != line B, check if matching line appears later.
    // Insert blank lines to align matching blocks.

    int i = 0;
    int j = 0;
    QStringList newLeft;
    QStringList newRight;

    while (i < left.size() || j < right.size())
    {
        QString lLine = (i < left.size()) ? left[i] : QString();
        QString rLine = (j < right.size()) ? right[j] : QString();
        bool hasL = (i < left.size());
        bool hasR = (j < right.size());

        if (hasL && hasR && lLine == rLine)
        {
            newLeft.append(lLine);
            newRight.append(rLine);
            i++; j++;
        }
        else
        {
            // IMPROVEMENT: Check for Similarity before looking ahead.
            // If lines are "Similar" (e.g. same display text but different logic), align them immediately.
            // This forces the "Word Diff" logic to run on them, highlighting specific address changes
            // instead of showing a full red line vs full green line.
            if (hasL && hasR && IsSimilar(lLine, rLine))
            {
                newLeft.append(lLine);
                newRight.append(rLine);
                i++; j++;
                continue;
            }

            // Standard Diff Lookahead
            // Ensure start index is within bounds before calling IndexOf
            int idxInRight = -1;
            if (hasL)
            {
                for (int k = j; k < right.size(); ++k)
                {
                    if (right[k] == lLine) { idxInRight = k; break; }
                }
            }

            int idxInLeft = -1;
            if (hasR && i < left.size())
            {
                for (int k = i; k < left.size(); ++k)
                {
                    if (left[k] == rLine) { idxInLeft = k; break; }
                }
            }

            if (idxInRight != -1 && (idxInLeft == -1 || idxInRight - j < idxInLeft - i))
            {
                // Right inserted lines
                while (j < idxInRight)
                {
                    newLeft.append("");
                    newRight.append(right[j]);
                    j++;
                }
            }
            else if (idxInLeft != -1)
            {
                // Left had extra lines
                while (i < idxInLeft)
                {
                    newLeft.append(left[i]);
                    newRight.append("");
                    i++;
                }
            }
            else
            {
                // Both different and not similar - Treat as Block Replace
                newLeft.append(hasL ? lLine : "");
                newRight.append(hasR ? rLine : "");
                if (hasL) i++;
                if (hasR) j++;
            }
        }
    }
    left = newLeft;
    right = newRight;
}

// Heuristic to detect if two lines are "versions of the same line"
bool TextDiffDialog::IsSimilar(const QString &left, const QString &right)
{
    if (left.isEmpty() || right.isEmpty()) return false;

    // Strategy for Rich Presence: Match the Display Text (after the last '?')
    auto GetDisplay = [](const QString &s) -> QString {
        int idx = s.lastIndexOf('?');
        if (idx >= 0 && idx < s.length() - 1) return s.mid(idx + 1);
        return s;
    };

    QString txtL = GetDisplay(left);
    QString txtR = GetDisplay(right);

    // If the display text matches and is substantial (>2 chars), treat as aligned
    if (txtL.length() > 2 && txtL == txtR) return true;

    return false;
}

void TextDiffDialog::HighlightDifferences(const QStringList &leftLines, const QStringList &rightLines)
{
    QColor diffBack(50, 0, 0); // Dark Red background for changed line
    QColor addedBack(0, 50, 0); // Dark Green background

    // Text Highlight Colors (Brighter foregrounds for specific changes)
    QColor textRemove(255, 100, 100);
    QColor textAdd(100, 255, 100);

    if (palette().window().color().red() > 200)
    {
        diffBack = QColor(255, 200, 200);
        addedBack = QColor(200, 255, 200);
        textRemove = Qt::red;
        textAdd = Qt::green;
    }

    rtbLeft->clear();
    rtbRight->clear();

    int maxCount = std::max(leftLines.size(), rightLines.size());
    for (int i = 0; i < maxCount; i++)
    {
        QString left = (i < leftLines.size()) ? leftLines[i] : "";
        QString right = (i < rightLines.size()) ? rightLines[i] : "";

        if (left == right)
        {
            AppendText(rtbLeft, left + "\n", palette().text().color(), palette().base().color());
            AppendText(rtbRight, right + "\n", palette().text().color(), palette().base().color());
        }
        else
        {
            // Empty lines means strict addition/deletion of the whole line
            if (left.isEmpty())
            {
                AppendText(rtbLeft, "\n", palette().text().color(), palette().base().color()); // Spacer
                AppendText(rtbRight, right + "\n", palette().text().color(), addedBack);
            }
            else if (right.isEmpty())
            {
                AppendText(rtbLeft, left + "\n", palette().text().color(), diffBack);
                AppendText(rtbRight, "\n", palette().text().color(), palette().base().color()); // Spacer
            }
            else
            {
                // Content Diff (Intra-line)
                auto diffs = ComputeWordDiff(left, right);

                // Render Left
                for (const auto &d : diffs.LeftTokens)
                {
                    AppendText(rtbLeft, d.Text, d.IsMatch ? palette().text().color() : textRemove, diffBack, !d.IsMatch);
                }
                AppendText(rtbLeft, "\n", palette().text().color(), palette().base().color());

                // Render Right
                for (const auto &d : diffs.RightTokens)
                {
                    AppendText(rtbRight, d.Text, d.IsMatch ? palette().text().color() : textAdd, addedBack, !d.IsMatch);
                }
                AppendText(rtbRight, "\n", palette().text().color(), palette().base().color());
            }
        }
    }
}

void TextDiffDialog::AppendText(QTextEdit *box, const QString &text, const QColor &fore, const QColor &back, bool bold)
{
    QTextCursor cursor(box->textCursor());
    cursor.movePosition(QTextCursor::End);

    QTextCharFormat fmt;
    fmt.setForeground(fore);
    fmt.setBackground(back);
    if (bold) fmt.setFontWeight(QFont::Bold);

    cursor.insertText(text, fmt);
}

void TextDiffDialog::UpdateDiffStats(const QStringList &leftLines, const QStringList &rightLines)
{
    int added = 0;
    int removed = 0;
    int maxCount = std::max(leftLines.size(), rightLines.size());
    for (int i = 0; i < maxCount; i++)
    {
        QString l = (i < leftLines.size()) ? leftLines[i] : "";
        QString r = (i < rightLines.size()) ? rightLines[i] : "";
        if (l != r)
        {
            if (l.isEmpty()) added++;
            else if (r.isEmpty()) removed++;
            else { added++; removed++; }
        }
    }
    lblStats->setText(QString("Changes: +%1 / -%2 lines").arg(added).arg(removed));
}

// Tokenize by splitting but keeping delimiters to reconstruct accurately.
// \w includes alphanumeric. 0x1234 is a word.
TextDiffDialog::DiffResult TextDiffDialog::ComputeWordDiff(const QString &left, const QString &right)
{
    static const QRegularExpression pattern(R"([^\w]+)");
    auto Tokenize = [](const QString &s) -> QStringList {
        QStringList tokens;
        int lastPos = 0;
        auto it = pattern.globalMatch(s);
        while (it.hasNext())
        {
            auto m = it.next();
            if (m.capturedStart() > lastPos)
                tokens.append(s.mid(lastPos, m.capturedStart() - lastPos));
            tokens.append(m.captured(0));
            lastPos = m.capturedEnd();
        }
        if (lastPos < s.length())
            tokens.append(s.mid(lastPos));
        return tokens;
    };

    QStringList lTokens = Tokenize(left);
    QStringList rTokens = Tokenize(right);

    // LCS implementation with flattened single allocation for CPU cache efficiency
    int cols = rTokens.size() + 1;
    QVector<int> matrix((lTokens.size() + 1) * cols, 0);

    for (int i = 1; i <= lTokens.size(); i++)
    {
        for (int j = 1; j <= rTokens.size(); j++)
        {
            if (lTokens[i - 1] == rTokens[j - 1])
                matrix[i * cols + j] = matrix[(i - 1) * cols + (j - 1)] + 1;
            else
                matrix[i * cols + j] = std::max(matrix[(i - 1) * cols + j], matrix[i * cols + (j - 1)]);
        }
    }

    DiffResult res;
    QSet<int> matchedIndicesL;
    QSet<int> matchedIndicesR;

    int x = lTokens.size(), y = rTokens.size();
    while (x > 0 && y > 0)
    {
        if (lTokens[x - 1] == rTokens[y - 1])
        {
            matchedIndicesL.insert(x - 1);
            matchedIndicesR.insert(y - 1);
            x--; y--;
        }
        else if (matrix[(x - 1) * cols + y] > matrix[x * cols + (y - 1)]) x--;
        else y--;
    }

    for (int i = 0; i < lTokens.size(); i++)
        res.LeftTokens.append(DiffToken{ lTokens[i], matchedIndicesL.contains(i) });

    for (int i = 0; i < rTokens.size(); i++)
        res.RightTokens.append(DiffToken{ rTokens[i], matchedIndicesR.contains(i) });

    return res;
}

} // namespace RALogicEditor
