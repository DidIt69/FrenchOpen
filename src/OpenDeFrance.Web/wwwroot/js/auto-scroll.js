// Auto-scroll marquee for text overflowing its container
window.autoScrollManager = {
    observer: null,
    intervalId: null,

    init: function () {
        this.update();

        // Observer pour détecter dynamiquement les changements dans le DOM (re-render Blazor)
        if (!this.observer && typeof MutationObserver !== 'undefined') {
            this.observer = new MutationObserver(() => {
                this.update();
            });
            this.observer.observe(document.body, { childList: true, subtree: true, characterData: true });

            window.addEventListener('resize', () => this.update());
        }

        // Vérification périodique légère pour assurer l'animation continue sur les écrans TV / dashboards
        if (!this.intervalId) {
            this.intervalId = setInterval(() => this.update(), 2000);
        }
    },

    update: function () {
        const containers = document.querySelectorAll('.auto-scroll-container');
        containers.forEach(container => {
            const content = container.querySelector('.auto-scroll-content') || container;
            const containerWidth = container.clientWidth || container.getBoundingClientRect().width;
            const contentWidth = Math.max(content.scrollWidth, content.getBoundingClientRect().width);
            const diff = contentWidth - containerWidth;

            if (diff > 3) {
                const distance = Math.ceil(diff + 12); // marge de confort
                const duration = Math.max(4, distance / 22); // vitesse de défilement fluide et lisible (~22px/s)
                content.style.setProperty('--scroll-distance', `-${distance}px`);
                content.style.setProperty('--scroll-duration', `${duration}s`);
                container.classList.add('is-overflowing');
            } else {
                container.classList.remove('is-overflowing');
                content.style.removeProperty('--scroll-distance');
                content.style.removeProperty('--scroll-duration');
            }
        });
    }
};

// Initialisation au chargement
if (typeof document !== 'undefined') {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.autoScrollManager.init());
    } else {
        window.autoScrollManager.init();
    }
    window.addEventListener('load', () => window.autoScrollManager.init());
    document.addEventListener('enhancedload', () => window.autoScrollManager.update());
}
