#pragma once

#include "../../Models/ValidationModels.h"

#include <QComboBox>
#include <QLabel>
#include <QList>
#include <QMenu>
#include <QPushButton>
#include <QTreeWidget>
#include <QWidget>

namespace RALE {

class CodeReviewWidget : public QWidget {
    Q_OBJECT

  public:
    explicit CodeReviewWidget(QWidget* parent = nullptr);

    void SetIssues(const QList<ValidationIssue>& issues);

    int FilterIndex() const;
    void SetFilterIndex(int index);

  signals:
    void IssueSelected(const ValidationIssue& issue);
    void SuggestedFixRequested(const ValidationIssue& issue);

  protected:
    bool eventFilter(QObject* watched, QEvent* event) override;
    void resizeEvent(QResizeEvent* event) override;

  private slots:
    void onFilterChanged();
    void onItemClicked(QTreeWidgetItem* item, int column);
    void onItemDoubleClicked(QTreeWidgetItem* item, int column);
    void onSelectionChanged();
    void onSuggestedFixClicked();
    void onHeaderSectionResized(int logicalIndex, int oldSize, int newSize);

  private:
    QComboBox* cmbFilter = nullptr;
    QPushButton* btnSuggestedFix = nullptr;
    QLabel* lblStatus = nullptr;
    QLabel* lblHeader = nullptr;
    QTreeWidget* listIssues = nullptr;
    QMenu* _contextMenu = nullptr;
    QAction* _actSuggestedFix = nullptr;
    QAction* _actGoToIssue = nullptr;
    QAction* _actOpenLink = nullptr;

    QList<ValidationIssue> _allIssues;
    QList<ValidationIssue> _filteredIssues;
    bool _isAdjustingColumns = false;

    void InitializeUI();
    void ApplyFilter();
    void UpdateStatus(int count);
    void UpdateSuggestedFixButton();
    void FitColumnsToViewport();
    QString GetSeverityIcon(FeedbackSeverity severity) const;
    const ValidationIssue* CurrentSelectedIssue() const;
    void TriggerIssueNavigation();
};

} // namespace RALE
