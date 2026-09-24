// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Bootstrap JS was removed as part of the DaisyUI theme migration. The Identity-scaffolded pages
// still mark dismissible status-message alerts with the old `data-bs-dismiss="alert"` attribute;
// this replaces that behaviour by hiding the alert on click instead of relying on Bootstrap's JS.
document.addEventListener("click", function (event) {
    var dismissTarget = event.target.closest('[data-bs-dismiss="alert"]');
    if (dismissTarget) {
        var alertEl = dismissTarget.closest(".alert");
        if (alertEl) {
            alertEl.classList.add("hidden");
        }
    }
});

// Single shared modal (Pages/Shared/_Layout.cshtml) used by every "gear" button on the Games/
// MyGames/User games-list tables to summarize a game's setup/settings - event-delegated so it
// works for any number of rows (the User profile page alone can have 900+) without per-row
// listeners or per-row <dialog> elements bloating the DOM.
function formatLocalDateTime(value) {
    return new Date(value).toLocaleString(undefined, {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit"
    });
}

function formatLocalDateTimeToMinute(value) {
    var date = new Date(value);
    if (date.getSeconds() >= 30) {
        date.setMinutes(date.getMinutes() + 1);
    }
    date.setSeconds(0, 0);

    return date.toLocaleString(undefined, {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
    });
}

document.querySelectorAll("[data-local-datetime-minute]").forEach(function (element) {
    var timestamp = element.getAttribute("datetime") || element.dataset.localDatetimeMinute;
    if (timestamp) {
        element.textContent = formatLocalDateTimeToMinute(timestamp);
    }
});

document.addEventListener("click", function (event) {
    var gearButton = event.target.closest(".js-game-settings-btn");
    if (!gearButton) {
        return;
    }

    var modal = document.getElementById("game-settings-modal");
    if (!modal) {
        return;
    }

    modal.querySelector("#game-settings-modal-title").textContent = gearButton.dataset.gameName || "";
    var ownerSection = modal.querySelector("#game-settings-modal-owner-section");
    var isFaceless = gearButton.dataset.isFaceless === "True";
    modal.querySelector("#game-settings-modal-owner").textContent = gearButton.dataset.ownerName || "-";
    ownerSection.classList.toggle("hidden", isFaceless);
    var createdAt = gearButton.dataset.createdAt || "";
    modal.querySelector("#game-settings-modal-created-at").textContent = createdAt ? formatLocalDateTimeToMinute(createdAt) : "-";
    var lastActiveAtSection = modal.querySelector("#game-settings-modal-last-active-at-section");
    var lastActiveAt = gearButton.dataset.lastActiveAt || "";
    modal.querySelector("#game-settings-modal-last-active-at").textContent = lastActiveAt ? formatLocalDateTime(lastActiveAt) : "";
    lastActiveAtSection.classList.toggle("hidden", !lastActiveAt);
    modal.querySelector("#game-settings-modal-setup").textContent = gearButton.dataset.setupName || "";
    modal.querySelector("#game-settings-modal-players").textContent = gearButton.dataset.playerCount || "";
    var roundLabel = modal.querySelector("#game-settings-modal-round-label");
    var roundValue = modal.querySelector("#game-settings-modal-round");
    var hasRound = Boolean(gearButton.dataset.round);
    roundValue.textContent = gearButton.dataset.round || "";
    roundLabel.classList.toggle("hidden", !hasRound);
    roundValue.classList.toggle("hidden", !hasRound);
    var statusFooter = modal.querySelector("#game-settings-modal-status-footer");
    var statusLabel = modal.querySelector("#game-settings-modal-status-label");
    var statusValue = modal.querySelector("#game-settings-modal-status-value");
    var statusCrown = modal.querySelector("#game-settings-modal-status-crown");
    var winner = gearButton.dataset.winner || "";
    var waitingFor = gearButton.dataset.waitingFor || "";
    var showingWinner = Boolean(winner);
    var footerLabel = showingWinner ? "Winner:" : (waitingFor ? "Waiting for" : "");
    var footerValue = winner || waitingFor;
    statusLabel.textContent = footerLabel;
    statusValue.textContent = footerValue;
    statusCrown.hidden = !showingWinner;
    statusFooter.classList.toggle("hidden", !footerValue);

    var settingsList = modal.querySelector("#game-settings-modal-settings");
    var noSettingsMessage = modal.querySelector("#game-settings-modal-no-settings");
    settingsList.innerHTML = "";
    var settings = (gearButton.dataset.settings || "").split("|").filter(function (s) { return s.length > 0; });
    if (settings.length === 0) {
        noSettingsMessage.classList.remove("hidden");
    } else {
        noSettingsMessage.classList.add("hidden");
        settings.forEach(function (label) {
            var li = document.createElement("li");
            li.textContent = label;
            settingsList.appendChild(li);
        });
    }

    modal.showModal();
});

// Persist the open/collapsed state of the <details> "collapse" sections on Games/MyGames/User
// (games/cancelled games/previously participated games), keyed by a per-section
// `data-collapse-key` attribute set in the Razor markup. Only writes a cookie on toggle - the
// *reverse* direction (applying the saved state on load) is done server-side, by
// CollapseStateExtensions.IsCollapseSectionOpen reading this same cookie and rendering the
// correct `open` attribute directly into the initial HTML. An earlier version of this feature
// used localStorage and re-applied the state client-side via JS after the page had already
// painted with the server's default state, which caused a visible layout shift/jank (the section
// would render open, then immediately snap shut once the script ran) - baking the state into the
// server render instead avoids any client-side recalculation entirely.
document.addEventListener(
    "toggle",
    function (event) {
        var details = event.target;
        if (!(details instanceof HTMLDetailsElement)) {
            return;
        }
        var key = details.dataset.collapseKey;
        if (!key) {
            return;
        }
        var oneYearInSeconds = 60 * 60 * 24 * 365;
        document.cookie =
            "snr_collapse_" + key + "=" + (details.open ? "open" : "closed") +
            "; path=/; max-age=" + oneYearInSeconds + "; samesite=lax";
    },
    true // the native "toggle" event does not bubble, so this must be a capturing listener
);
