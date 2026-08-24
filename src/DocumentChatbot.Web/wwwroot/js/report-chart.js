(() => {
    "use strict";

    let chart = null;

    function renderQuestionChart() {
        const canvas = document.querySelector("[data-question-chart]");
        if (!canvas || typeof Chart === "undefined") return;

        chart?.destroy();
        const labels = JSON.parse(canvas.dataset.labels || "[]");
        const values = JSON.parse(canvas.dataset.values || "[]");

        chart = new Chart(canvas, {
            type: "line",
            data: {
                labels,
                datasets: [{
                    label: "Questions",
                    data: values,
                    borderColor: "#d9d9d9",
                    backgroundColor: "rgba(125, 159, 181, 0.18)",
                    pointBackgroundColor: "#7d9fb5",
                    pointBorderColor: "#171717",
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    borderWidth: 3,
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: context => `${context.parsed.y} questions` } }
                },
                scales: {
                    x: { ticks: { color: "#9a9a9a" }, grid: { display: false } },
                    y: { beginAtZero: true, ticks: { precision: 0, color: "#9a9a9a" }, grid: { color: "#303030" } }
                }
            }
        });
    }

    document.addEventListener("DOMContentLoaded", renderQuestionChart);
    document.addEventListener("report-content-updated", renderQuestionChart);
})();
