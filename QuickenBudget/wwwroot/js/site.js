/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
const commonFontSize = '16px';
const commonFont = '16px sans-serif';

// Helper - compute the biggest rect size that contains every label in the list
function getMaxLabelSize(labels, fontSize = "16px") {
    const tempSvg = d3
        .select("body")
        .append("svg")
        .attr("visibility", "hidden");
    const result = labels.reduce((maxSize, label) => {
        const text = tempSvg
            .append("text")
            .style("font-size", fontSize)
            .text(label);
        const bbox = text.node().getBBox();
        return { width: Math.max(maxSize.width, bbox.width), height: Math.max(maxSize.height, bbox.height) }
    }, {width: 0, height: 0})
    tempSvg.remove();
    return result;
}

// Helper: show tooltip on a d3 chart from event and a data item (works on barcharts)
function showTooltip(tooltipId, event, d) {
   // Normalize coordinates so this works for both mouse and touch events
    let pageX;
    let pageY;
    if (event && event.touches && event.touches.length > 0) {
        const touch = event.touches[0];
        pageX = touch.pageX !== undefined ? touch.pageX : touch.clientX + window.pageXOffset;
        pageY = touch.pageY !== undefined ? touch.pageY : touch.clientY + window.pageYOffset;
    } else if (event && event.changedTouches && event.changedTouches.length > 0) {
        const touch = event.changedTouches[0];
        pageX = touch.pageX !== undefined ? touch.pageX : touch.clientX + window.pageXOffset;
        pageY = touch.pageY !== undefined ? touch.pageY : touch.clientY + window.pageYOffset;
    } else {
        pageX = event.pageX;
        pageY = event.pageY;
    }

    const tooltip = d3.select(`#${tooltipId}`);
    tooltip.style("display", "block")
        .style("left", (pageX + 12) + "px")
        .style("top", (pageY - 12) + "px")
        .html(d.html());
}

// Helper: hide tooltip
function hideTooltip(tooltipId) {
    d3.select(`#${tooltipId}`).style("display", "none");
}

function encodeToHTML(str) {
    // Replace special HTML characters with entities
    const encoded = str
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");

    // Replace newlines with <br> for HTML display
    return encoded.replace(/\r?\n/g, "<br>");
}

document.addEventListener("DOMContentLoaded", () => {
    const indicator = document.getElementById("reloadStatusIndicator");
    const recentMessagesModal = document.getElementById("recentMessagesModal");
    const recentMessagesList = document.getElementById("recentMessagesList");
    const pageRefreshTime = document.body?.dataset?.pageRefreshTime;
    const modal = recentMessagesModal ? new bootstrap.Modal(recentMessagesModal) : null;

    if (!indicator || !pageRefreshTime) {
        return;
    }

    const showMessages = async () => {
        if (!modal || !recentMessagesList) {
            return;
        }

        const response = await fetch("/api/recentMessages", { cache: "no-store" });
        if (!response.ok) {
            throw new Error(`recentMessages request failed with ${response.status}`);
        }

        const messages = await response.json();
        recentMessagesList.textContent = "";

        if (!Array.isArray(messages) || messages.length === 0) {
            const item = document.createElement("li");
            item.textContent = "No warnings or errors were captured during the most recent load.";
            recentMessagesList.appendChild(item);
        } else {
            for (const message of messages) {
                const item = document.createElement("li");
                item.innerHTML = encodeToHTML(message);
                recentMessagesList.appendChild(item);
            }
        }

        modal.show();
    };

    indicator.addEventListener("click", async () => {
        const action = indicator.dataset.action;
        if (action === "reload") {
            window.location.reload();
            return;
        }

        if (action === "messages") {
            try {
                await showMessages();
            } catch (error) {
                console.warn("Unable to display recent reload messages.", error);
            }
        }
    });

    const updateIndicator = async () => {
        if (indicator.dataset.action !== "reload") {
            try {
                const response = await fetch(`/api/status?since=${encodeURIComponent(pageRefreshTime)}`, { cache: "no-store" });
                if (!response.ok) {
                    return;
                }

                const status = (await response.text()).trim().toLowerCase();
                indicator.classList.remove("d-none", "reload-status", "error-status");

                switch (status) {
                    case "reload":
                        indicator.dataset.action = "reload";
                        indicator.textContent = "Reload page";
                        indicator.classList.add("reload-status");
                        break;
                    case "errors":
                        indicator.dataset.action = "messages";
                        indicator.textContent = "Show reload errors";
                        indicator.classList.add("error-status");
                        break;
                    default:
                        indicator.dataset.action = "";
                        indicator.textContent = "";
                        indicator.classList.add("d-none");
                        break;
                }
            } catch (error) {
                console.warn("Status polling failed.", error);
            }
        }
    };

    updateIndicator();
    window.setInterval(updateIndicator, 10000);
});

