// Module de Scan QR Code / Code-barres par caméra pour le poste de pesée
window.qrScanner = {
    activeStream: null,
    animFrameId: null,
    detector: null,

    isSupported: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    },

    startScanner: async function (videoElementId, dotNetHelper, methodName) {
        try {
            this.stopScanner();

            const video = document.getElementById(videoElementId);
            if (!video) {
                console.warn('Élément vidéo introuvable:', videoElementId);
                return { success: false, error: 'Élément vidéo introuvable' };
            }

            // Préférer la caméra arrière (environment) sur mobile
            const constraints = {
                video: {
                    facingMode: { ideal: 'environment' },
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                },
                audio: false
            };

            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            this.activeStream = stream;
            video.srcObject = stream;
            await video.play();

            // Vérifier si l'API BarcodeDetector est disponible nativement (Chrome, Edge, Safari iOS 17+)
            if ('BarcodeDetector' in window) {
                try {
                    const formats = await window.BarcodeDetector.getSupportedFormats();
                    if (formats.includes('qr_code')) {
                        this.detector = new window.BarcodeDetector({ formats: ['qr_code', 'code_128', 'ean_13'] });
                    }
                } catch (e) {
                    console.warn('BarcodeDetector error:', e);
                }
            }

            let isProcessing = false;
            let lastScanTime = 0;

            const scanLoop = async () => {
                if (!this.activeStream) return;

                const now = Date.now();
                // Scanner toutes les 250ms pour économiser CPU
                if (now - lastScanTime > 250 && !isProcessing && video.readyState === video.HAVE_ENOUGH_DATA) {
                    isProcessing = true;
                    try {
                        if (this.detector) {
                            const barcodes = await this.detector.detect(video);
                            if (barcodes && barcodes.length > 0) {
                                const rawValue = barcodes[0].rawValue;
                                if (rawValue) {
                                    // Retour haptique sur smartphone
                                    if ('vibrate' in navigator) {
                                        try { navigator.vibrate(80); } catch (e) { }
                                    }
                                    dotNetHelper.invokeMethodAsync(methodName, rawValue);
                                    // Petite pause après détection
                                    await new Promise(r => setTimeout(r, 1200));
                                }
                            }
                        }
                    } catch (err) {
                        // ignore frame error
                    } finally {
                        isProcessing = false;
                        lastScanTime = Date.now();
                    }
                }

                this.animFrameId = requestAnimationFrame(scanLoop);
            };

            this.animFrameId = requestAnimationFrame(scanLoop);
            return { success: true };
        } catch (err) {
            console.error('Erreur ouverture caméra:', err);
            return { success: false, error: err.message || 'Accès à la caméra refusé ou indisponible' };
        }
    },

    vibrate: function (pattern) {
        if ('vibrate' in navigator) {
            try { navigator.vibrate(pattern || [50, 40, 80]); } catch (e) { }
        }
    },

    stopScanner: function () {
        if (this.animFrameId) {
            cancelAnimationFrame(this.animFrameId);
            this.animFrameId = null;
        }
        if (this.activeStream) {
            this.activeStream.getTracks().forEach(track => {
                try { track.stop(); } catch (e) { }
            });
            this.activeStream = null;
        }
    }
};
