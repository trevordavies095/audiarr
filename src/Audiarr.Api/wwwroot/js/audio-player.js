// Audio Player JavaScript Module

export function initializeAudioPlayer(audioElement) {
    if (!audioElement) return;
    
    // Set up event listeners
    audioElement.addEventListener('ended', () => {
        console.log('Track ended');
    });
    
    audioElement.addEventListener('error', (e) => {
        const error = e.target.error;
        let errorMessage = 'Unknown audio error';
        
        if (error) {
            switch(error.code) {
                case error.MEDIA_ERR_ABORTED:
                    errorMessage = 'Audio playback aborted';
                    break;
                case error.MEDIA_ERR_NETWORK:
                    errorMessage = 'Network error while loading audio';
                    break;
                case error.MEDIA_ERR_DECODE:
                    errorMessage = 'Audio decoding error';
                    break;
                case error.MEDIA_ERR_SRC_NOT_SUPPORTED:
                    errorMessage = 'Audio format not supported or URL invalid';
                    break;
            }
        }
        
        console.error('Audio error:', errorMessage, 'URL:', audioElement.src);
    });
    
    // Remove CORS setting as it can cause issues with local streaming
    // audioElement.crossOrigin = "anonymous";
}

export function loadTrack(audioElement, url) {
    if (!audioElement) return;
    
    // Ensure URL is absolute
    if (url && !url.startsWith('http') && !url.startsWith('//')) {
        // Convert relative URL to absolute
        const baseUrl = window.location.origin;
        url = baseUrl + (url.startsWith('/') ? url : '/' + url);
    }
    
    console.log('Loading track:', url);
    audioElement.src = url;
    audioElement.load();
}

export function play(audioElement) {
    if (!audioElement) return;
    
    const playPromise = audioElement.play();
    
    if (playPromise !== undefined) {
        playPromise.catch(error => {
            console.error('Play error:', error);
        });
    }
}

export function pause(audioElement) {
    if (!audioElement) return;
    
    audioElement.pause();
}

export function seek(audioElement, time) {
    if (!audioElement) return;
    
    audioElement.currentTime = time;
}

export function setVolume(audioElement, volume) {
    if (!audioElement) return;
    
    audioElement.volume = Math.max(0, Math.min(1, volume));
}

export function getCurrentTime(audioElement) {
    if (!audioElement) return 0;
    
    return audioElement.currentTime;
}

export function getDuration(audioElement) {
    if (!audioElement) return 0;
    
    return audioElement.duration || 0;
}

export function cleanup(audioElement) {
    if (!audioElement) return;
    
    audioElement.pause();
    audioElement.src = '';
    audioElement.load();
}