// Clipboard Helper Functions for Audiarr
// Handles clipboard operations with fallback for non-HTTPS environments

window.clipboardHelper = {
    /**
     * Copy text to clipboard with fallback for non-secure contexts
     * @param {string} text - The text to copy
     * @param {string} elementId - Optional element ID to use for fallback selection
     * @returns {Promise<boolean>} - Returns true if successful, false otherwise
     */
    copyToClipboard: async function(text, elementId) {
        // First try the modern clipboard API
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                console.log('Text copied using clipboard API');
                return true;
            } catch (err) {
                console.warn('Clipboard API failed:', err);
            }
        }
        
        // Fallback method using a temporary textarea
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.top = '-9999px';
        textarea.style.left = '-9999px';
        textarea.setAttribute('readonly', '');
        
        document.body.appendChild(textarea);
        
        try {
            // Select the text
            textarea.select();
            textarea.setSelectionRange(0, 99999); // For mobile devices
            
            // Try to copy
            const success = document.execCommand('copy');
            
            if (success) {
                console.log('Text copied using fallback method');
            } else {
                console.warn('Fallback copy method failed');
                
                // If we have an element ID, select it for manual copy
                if (elementId) {
                    const element = document.getElementById(elementId);
                    if (element) {
                        element.select();
                        element.setSelectionRange(0, 99999);
                        console.log('Text selected for manual copy');
                    }
                }
            }
            
            return success;
        } catch (err) {
            console.error('Error copying text:', err);
            
            // Last resort: select text in the provided element
            if (elementId) {
                const element = document.getElementById(elementId);
                if (element) {
                    element.select();
                    element.setSelectionRange(0, 99999);
                    console.log('Text selected for manual copy (error fallback)');
                }
            }
            
            return false;
        } finally {
            document.body.removeChild(textarea);
        }
    },
    
    /**
     * Check if clipboard API is available
     * @returns {boolean} - True if clipboard API is available
     */
    isClipboardAvailable: function() {
        return navigator.clipboard && window.isSecureContext;
    },
    
    /**
     * Select text in an input element
     * @param {string} elementId - The ID of the input element
     */
    selectText: function(elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.select();
            element.setSelectionRange(0, 99999); // For mobile devices
            element.focus();
        }
    }
};

// Make functions available to Blazor
window.copyToClipboard = window.clipboardHelper.copyToClipboard;
window.selectText = window.clipboardHelper.selectText;
window.isClipboardAvailable = window.clipboardHelper.isClipboardAvailable;