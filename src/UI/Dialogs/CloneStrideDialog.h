#pragma once

#include <QDialog>
#include <QLabel>
#include <QLineEdit>
#include <QList>
#include <QPushButton>
#include <QSpinBox>
#include <QTableWidget>

#include <cstdint>

namespace RALE {

struct GeneratedStrideNote {
    int64_t address = 0;
    QString addressHex = "";
    QString title = "";
    QString noteText = "";
};

// Dialog providing an Excel-style sequential note cloner for array structures
// and repeating memory tables (e.g. Level 1..N stats, Player 1..N structs).
class CloneStrideDialog : public QDialog {
    Q_OBJECT

  public:
    explicit CloneStrideDialog(const QString& currentAddress, const QString& currentNoteText, QWidget* parent = nullptr);
    ~CloneStrideDialog() override;

    QList<GeneratedStrideNote> getGeneratedNotes() const;

  protected:
    void done(int r) override;

  private slots:
    void onParametersChanged();
    void onPasteNamesFromClipboard();
    void onTableItemChanged(QTableWidgetItem* item);
    void onGenerateClicked();

  private:
    int64_t m_currentAddress = 0;
    int m_addressHexLen = 8;
    QString m_originalNoteText;
    QString m_originalBody;
    QString m_detectedFirstName;
    bool m_isUpdatingTable = false;

    QLineEdit* m_baseAddressEdit = nullptr;
    QSpinBox* m_strideSpin = nullptr;
    QLabel* m_strideHexLabel = nullptr;
    QSpinBox* m_countSpin = nullptr;
    QLineEdit* m_templateEdit = nullptr;
    QPushButton* m_pasteNamesBtn = nullptr;

    QTableWidget* m_previewTable = nullptr;
    QPushButton* m_generateBtn = nullptr;
    QPushButton* m_cancelBtn = nullptr;

    QList<GeneratedStrideNote> m_generatedNotes;

    void setupLayout();
    void parseInitialNote(uint32_t& outStride, QString& outTemplate, QString& outFirstName);
    void rebuildTableRows();
    QString formatTitle(int index, uint32_t offsetBytes, const QString& customName) const;
    int64_t getBaseAddress() const;
    void loadPositionSettings();
    void savePositionSettings();
};

} // namespace RALE
