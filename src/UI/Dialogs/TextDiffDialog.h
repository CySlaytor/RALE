#pragma once

#include <QDialog>
#include <QTextEdit>
#include <QLabel>
#include <QStringList>

namespace RALE {

class TextDiffDialog : public QDialog
{
    Q_OBJECT

public:
    TextDiffDialog(
        const QString &title,
        const QString &leftText,
        const QString &rightText,
        QWidget *parent = nullptr);

private:
    QTextEdit *rtbLeft = nullptr;
    QTextEdit *rtbRight = nullptr;
    QLabel *lblLeft = nullptr;
    QLabel *lblRight = nullptr;
    QLabel *lblStats = nullptr;

    void InitializeUI();
    void ApplyTheme();
    void AlignDiffLists(QStringList &left, QStringList &right);
    bool IsSimilar(const QString &left, const QString &right);
    void HighlightDifferences(const QStringList &leftLines, const QStringList &rightLines);
    void UpdateDiffStats(const QStringList &leftLines, const QStringList &rightLines);

    // --- Basic Word Diff Algorithm ---
    // Added default empty string initialization to avoid CS8618 warning
    struct DiffToken {
        QString Text = "";
        bool IsMatch = false;
    };

    struct DiffResult {
        QList<DiffToken> LeftTokens;
        QList<DiffToken> RightTokens;
    };

    DiffResult ComputeWordDiff(const QString &left, const QString &right);
    void AppendText(QTextEdit *box, const QString &text, const QColor &fore, const QColor &back, bool bold = false);
};

} // namespace RALE
