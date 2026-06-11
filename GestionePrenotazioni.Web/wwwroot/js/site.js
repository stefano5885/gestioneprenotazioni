const menuStorageKey = "gestione-prenotazioni.menu-collapsed";

function setMenuCollapsed(collapsed) {
  document.body.classList.toggle("menu-collapsed", collapsed);
  const toggle = document.querySelector("[data-menu-toggle]");
  if (toggle) {
    toggle.setAttribute("aria-expanded", String(!collapsed));
    toggle.setAttribute("title", collapsed ? "Espandi menu" : "Comprimi menu");
    toggle.setAttribute("aria-label", collapsed ? "Espandi menu" : "Comprimi menu");
  }
}

function uppercaseNameField(input) {
  const selectionStart = input.selectionStart;
  const selectionEnd = input.selectionEnd;
  const upperValue = input.value.toLocaleUpperCase("it-IT");

  if (input.value !== upperValue) {
    input.value = upperValue;
    if (selectionStart !== null && selectionEnd !== null) {
      input.setSelectionRange(selectionStart, selectionEnd);
    }
  }
}

function duplicateCheckUrl(form, nameInput) {
  const url = new URL(window.location.href);
  const formData = new FormData(form);
  url.search = "";
  url.searchParams.set("handler", "SimilarReservation");
  url.searchParams.set("bookerName", nameInput.value.trim());

  [
    "SelectedOrganizationId",
    "SelectedEventId",
    "SelectedDateId",
    "SelectedShiftId"
  ].forEach((key) => {
    const value = formData.get(key);
    if (value !== null && value.toString().length > 0) {
      url.searchParams.set(key, value.toString());
    }
  });

  return url;
}

function setupFlashMessage() {
  const flash = document.querySelector("[data-flash-message]");
  if (!flash) {
    return;
  }

  let dismissed = false;
  const controller = new AbortController();
  const dismiss = () => {
    if (dismissed) {
      return;
    }

    dismissed = true;
    controller.abort();
    flash.classList.add("is-hiding");
    window.setTimeout(() => flash.remove(), 220);
  };

  const dismissFromPageAction = (event) => {
    if (event.target instanceof Element && event.target.closest("[data-flash-message]")) {
      return;
    }

    dismiss();
  };

  window.setTimeout(dismiss, 8000);
  flash.querySelector("[data-flash-close]")?.addEventListener("click", dismiss);
  ["pointerdown", "keydown", "input", "change", "submit", "wheel", "touchmove"].forEach((eventName) => {
    document.addEventListener(eventName, dismissFromPageAction, {
      capture: true,
      passive: true,
      signal: controller.signal
    });
  });
}

document.addEventListener("DOMContentLoaded", () => {
  setupFlashMessage();

  const menu = document.querySelector(".app-menu");
  const savedMenuState = localStorage.getItem(menuStorageKey);
  const defaultCollapsed = menu?.dataset.compactWorkflow === "true";
  setMenuCollapsed(savedMenuState === null ? defaultCollapsed : savedMenuState === "true");

  document.querySelector("[data-menu-toggle]")?.addEventListener("click", () => {
    const collapsed = !document.body.classList.contains("menu-collapsed");
    localStorage.setItem(menuStorageKey, String(collapsed));
    setMenuCollapsed(collapsed);
  });

  document.querySelector("[data-booking-shift-select]")?.addEventListener("change", (event) => {
    if (event.target instanceof HTMLSelectElement && event.target.value) {
      window.location.assign(event.target.value);
    }
  });

  document.querySelectorAll("[data-context-form]").forEach((form) => {
    const mustConfirm = form.dataset.confirmContextChange === "true";
    form.querySelectorAll("select").forEach((select) => {
      select.dataset.previousValue = select.value;

      select.addEventListener("focus", () => {
        select.dataset.previousValue = select.value;
      });

      select.addEventListener("change", () => {
        if (mustConfirm) {
          const confirmed = window.confirm("Confermi il cambio di organizzazione, evento, data o turno in accoglienza? La lista delle persone in arrivo verra aggiornata.");
          if (!confirmed) {
            select.value = select.dataset.previousValue || "";
            return;
          }
        }

        form.submit();
      });
    });
  });

  document.querySelectorAll("[data-uppercase-name]").forEach((input) => {
    if (input instanceof HTMLInputElement) {
      uppercaseNameField(input);
    }
  });

  document.addEventListener("input", (event) => {
    if (!(event.target instanceof HTMLInputElement) || !event.target.matches("[data-uppercase-name]")) {
      return;
    }

    uppercaseNameField(event.target);
    if (event.target.form) {
      delete event.target.form.dataset.duplicateConfirmed;
    }
  });

  document.querySelectorAll("[data-duplicate-check-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      if (form.dataset.duplicateConfirmed === "true") {
        form.dataset.duplicateConfirmed = "false";
        return;
      }

      const nameInput = form.querySelector("[data-booker-name]");
      if (!(nameInput instanceof HTMLInputElement)) {
        return;
      }

      uppercaseNameField(nameInput);
      if (!form.checkValidity()) {
        return;
      }

      event.preventDefault();
      try {
        const response = await fetch(duplicateCheckUrl(form, nameInput), {
          headers: { "X-Requested-With": "fetch" }
        });

        if (response.ok) {
          const result = await response.json();
          if (result.hasMatch && !window.confirm(result.message)) {
            nameInput.focus();
            return;
          }
        }
      } catch {
        // Il controllo duplicati supporta l'operatore: il server valida comunque al submit.
      }

      form.dataset.duplicateConfirmed = "true";
      form.requestSubmit();
    });
  });

  document.querySelectorAll("[data-live-filter-form]").forEach((form) => {
    const targetSelector = form.dataset.liveFilterTarget;
    const target = targetSelector ? document.querySelector(targetSelector) : null;
    if (!target) {
      return;
    }

    let debounceId = 0;
    let requestId = 0;
    let controller = null;

    const buildUrl = (includeHandler) => {
      const url = new URL(window.location.href);
      const formData = new FormData(form);
      url.search = "";
      if (includeHandler) {
        url.searchParams.set("handler", "ReservationList");
      }

      for (const [key, value] of formData.entries()) {
        if (value !== null && value.toString().length > 0) {
          url.searchParams.set(key, value);
        }
      }

      return url;
    };

    const refreshList = async () => {
      const currentRequestId = ++requestId;
      if (controller) {
        controller.abort();
      }

      controller = new AbortController();
      target.classList.add("loading");

      try {
        const response = await fetch(buildUrl(true), {
          headers: { "X-Requested-With": "fetch" },
          signal: controller.signal
        });

        if (!response.ok) {
          throw new Error(`Filtro non riuscito: ${response.status}`);
        }

        const html = await response.text();
        if (currentRequestId !== requestId) {
          return;
        }

        target.innerHTML = html;
        window.history.replaceState({}, "", buildUrl(false));
      } catch (error) {
        if (error.name !== "AbortError") {
          target.innerHTML = '<p class="notice">Impossibile aggiornare la lista. Riprova.</p>';
        }
      } finally {
        if (currentRequestId === requestId) {
          target.classList.remove("loading");
        }
      }
    };

    const scheduleRefresh = (delay) => {
      window.clearTimeout(debounceId);
      debounceId = window.setTimeout(refreshList, delay);
    };

    form.querySelectorAll("[data-live-filter-input]").forEach((input) => {
      input.addEventListener("input", () => scheduleRefresh(180));
      input.addEventListener("change", () => scheduleRefresh(0));
    });

    form.addEventListener("submit", (event) => {
      event.preventDefault();
      scheduleRefresh(0);
    });
  });

  document.querySelectorAll("[data-assign-form]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (form.dataset.hasAssignments !== "true") {
        return;
      }

      const confirmed = window.confirm("Ricalcolare le assegnazioni del turno? Le assegnazioni esistenti verranno sostituite e le modifiche manuali andranno perse.");
      if (!confirmed) {
        event.preventDefault();
        return;
      }

      const confirmInput = form.querySelector("[data-confirm-reassign]");
      if (confirmInput) {
        confirmInput.value = "true";
      }
    });
  });
});
