#include "CodeReviewWidget.h"

#include "../../Models/AssetModels.h"
#include "../Styles/ThemeManager.h"

#include <QHBoxLayout>
#include <QHeaderView>
#include <QSet>
#include <QVBoxLayout>

#include <algorithm>

namespace RALE {

CodeReviewWidget::CodeReviewWidget(QWidget* parent) : QWidget(parent) {
    setMinimumHeight(60);
    InitializeUI();

    cmbFilter->addItem("Show All Issues");
    cmbFilter->addItem("Logic Warnings & Errors");
    cmbFilter->addItem("Missing Code Notes");
    cmbFilter->setCurrentIndex(0);

    connect(cmbFilter, QOverload<int>::of(&QComboBox::currentIndexChanged), this, &CodeReviewWidget::onFilterChanged);

    connect(listIssues, &QTreeWidget::itemDoubleClicked, this, &CodeReviewWidget::onItemDoubleClicked);

    connect(listIssues, &QTreeWidget::itemActivated, this, &CodeReviewWidget::onItemActivated);
}

void CodeReviewWidget::InitializeUI() {
    auto* mainLayout = new QVBoxLayout(this);
    mainLayout->setContentsMargins(0, 0, 0, 0);
    mainLayout->setSpacing(0);

    auto* headerPanel = new QWidget(this);
    headerPanel->setObjectName("panelHeader");
    headerPanel->setFixedHeight(34);

    auto* headerLayout = new QHBoxLayout(headerPanel);
    headerLayout->setContentsMargins(5, 4, 5, 4);
    headerLayout->setSpacing(8);

    lblHeader = new QLabel("Code Review", headerPanel);
    QFont hf = lblHeader->font();
    hf.setBold(true);
    lblHeader->setFont(hf);

    cmbFilter = new QComboBox(headerPanel);
    cmbFilter->setFixedWidth(160);
    ThemeManager::SetupComboBox(cmbFilter, 14);

    lblStatus = new QLabel("No issues found.", headerPanel);
    lblStatus->setAlignment(Qt::AlignRight | Qt::AlignVCenter);

    headerLayout->addWidget(lblHeader);
    headerLayout->addWidget(cmbFilter);
    headerLayout->addStretch();
    headerLayout->addWidget(lblStatus);

    mainLayout->addWidget(headerPanel);

    listIssues = new QTreeWidget(this);
    listIssues->setHeaderLabels({"Sev", "Asset", "Title", "Description", "Ref"});
    listIssues->setRootIsDecorated(false);
    listIssues->setAllColumnsShowFocus(true);
    listIssues->setSelectionBehavior(QAbstractItemView::SelectRows);
    listIssues->setSelectionMode(QAbstractItemView::SingleSelection);

    listIssues->setColumnWidth(0, 35);
    listIssues->setColumnWidth(1, 150);
    listIssues->setColumnWidth(2, 150);
    listIssues->setColumnWidth(4, 50);
    listIssues->header()->setSectionResizeMode(3, QHeaderView::Stretch);

    mainLayout->addWidget(listIssues, 1);
}

int CodeReviewWidget::FilterIndex() const { return cmbFilter ? cmbFilter->currentIndex() : 0; }

void CodeReviewWidget::SetFilterIndex(int index) {
    if (cmbFilter && index >= 0 && index < cmbFilter->count()) {
        cmbFilter->setCurrentIndex(index);
    }
}

void CodeReviewWidget::SetIssues(const QList<ValidationIssue>& issues) {
    _allIssues = issues;
    ApplyFilter();
}

void CodeReviewWidget::onFilterChanged() { ApplyFilter(); }

void CodeReviewWidget::ApplyFilter() {
    // Identify the selected issue by its full contents (asset + title + description
    // + reference), not just asset/title: many issues share the same title.
    auto IssueKey = [](const ValidationIssue& issue) -> QString {
        return QString("%1|%2|%3|%4|%5")
            .arg(static_cast<int>(issue.Severity))
            .arg(issue.Asset != nullptr ? issue.Asset->ID : 0)
            .arg(issue.Title, issue.Description, issue.WikiUrl);
    };

    QString selectedKey;
    if (listIssues->currentItem() != nullptr) {
        int row = listIssues->indexOfTopLevelItem(listIssues->currentItem());
        if (row >= 0 && row < _filteredIssues.size())
            selectedKey = IssueKey(_filteredIssues[row]);
    }

    listIssues->setUpdatesEnabled(false);
    listIssues->clear();
    _filteredIssues.clear();

    int filterMode = cmbFilter->currentIndex();
    QSet<QString> seenMissingAddresses;

    auto sortedIssues = _allIssues;
    std::sort(sortedIssues.begin(), sortedIssues.end(), [](const ValidationIssue& a, const ValidationIssue& b) {
        if (a.Severity != b.Severity)
            return a.Severity > b.Severity;
        int aId = a.Asset != nullptr ? a.Asset->ID : 0;
        int bId = b.Asset != nullptr ? b.Asset->ID : 0;
        if (aId != bId)
            return aId < bId;
        return a.Title < b.Title;
    });

    for (int i = 0; i < sortedIssues.size(); i++) {
        const auto& issue = sortedIssues[i];
        bool isMissingNote = (issue.Title == "Undocumented Memory");

        if (filterMode == 1 && isMissingNote)
            continue;
        if (filterMode == 2 && !isMissingNote)
            continue;

        if (isMissingNote) {
            if (seenMissingAddresses.contains(issue.Description))
                continue;
            seenMissingAddresses.insert(issue.Description);
        }

        _filteredIssues.append(issue);
        int issueIndex = _filteredIssues.size() - 1;

        QString assetStr = issue.Asset != nullptr ? QString("%1 - %2").arg(issue.Asset->ID).arg(issue.Asset->Title) : "Global";

        auto* item = new QTreeWidgetItem();
        item->setText(0, GetSeverityIcon(issue.Severity));
        item->setText(1, assetStr);
        item->setText(2, issue.Title);
        item->setText(3, issue.Description);
        item->setText(4, !issue.WikiUrl.isEmpty() ? "Link" : "");

        item->setData(0, Qt::UserRole, issueIndex);

        if (issue.Severity == FeedbackSeverity::Error) {
            item->setForeground(0, Qt::red);
            item->setForeground(2, Qt::red);
        } else if (issue.Severity == FeedbackSeverity::Warning) {
            item->setForeground(0, QColor(255, 140, 0));
            item->setForeground(2, QColor(255, 140, 0));
        }

        if (!issue.WikiUrl.isEmpty()) {
            bool isDark = (ThemeManager::CurrentTheme() == ThemeManager::Theme::Dark);
            item->setForeground(4, isDark ? QColor(88, 166, 255) : QColor(0, 102, 204));
            QFont f = item->font(4);
            f.setUnderline(true);
            item->setFont(4, f);
        }

        listIssues->addTopLevelItem(item);

        if (!selectedKey.isEmpty() && IssueKey(issue) == selectedKey) {
            listIssues->setCurrentItem(item);
            listIssues->scrollToItem(item);
        }
    }

    listIssues->setUpdatesEnabled(true);
    UpdateStatus(listIssues->topLevelItemCount());
}

void CodeReviewWidget::UpdateStatus(int count) {
    lblStatus->setText(count == 0 ? "No issues found." : QString("%1 issue(s) found.").arg(count));
    lblStatus->setStyleSheet(count == 0 ? "color: green;" : "");
}

QString CodeReviewWidget::GetSeverityIcon(FeedbackSeverity severity) const {
    switch (severity) {
    case FeedbackSeverity::Error:
        return QString::fromUtf8("❌");
    case FeedbackSeverity::Warning:
        return QString::fromUtf8("⚠️");
    case FeedbackSeverity::Info:
        return QString::fromUtf8("ℹ️");
    default:
        return "";
    }
}

void CodeReviewWidget::TriggerIssueNavigation() {
    auto* item = listIssues->currentItem();
    if (item != nullptr) {
        int idx = item->data(0, Qt::UserRole).toInt();
        if (idx >= 0 && idx < _filteredIssues.size()) {
            emit IssueSelected(_filteredIssues[idx]);
        }
    }
}

void CodeReviewWidget::onItemDoubleClicked(QTreeWidgetItem* /*item*/, int /*column*/) {
    // Double-clicking anywhere (including the Ref column) navigates to the issue,
    // matching the C# reviewer; the wiki URL is informational only.
    TriggerIssueNavigation();
}

void CodeReviewWidget::onItemActivated(QTreeWidgetItem* /*item*/, int /*column*/) { TriggerIssueNavigation(); }

} // namespace RALE
