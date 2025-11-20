// EstateArchitect - Enhanced User Experience

document.addEventListener('DOMContentLoaded', function() {
    // Add smooth scroll behavior for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
    });

    // Add loading state to form submissions
    document.querySelectorAll('form').forEach(form => {
        form.addEventListener('submit', function(e) {
            const button = this.querySelector('button[type="submit"]');
            if (button && !button.disabled) {
                button.disabled = true;
                const originalText = button.innerHTML;
                button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
                
                // Re-enable after 2 seconds as fallback
                setTimeout(() => {
                    button.disabled = false;
                    button.innerHTML = originalText;
                }, 2000);
            }
        });
    });

    // Add tooltip animations
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Highlight new content with fade-in animation
    const newContent = document.querySelectorAll('.alert, .card');
    newContent.forEach((element, index) => {
        element.style.opacity = '0';
        element.style.transform = 'translateY(20px)';
        setTimeout(() => {
            element.style.transition = 'all 0.5s ease';
            element.style.opacity = '1';
            element.style.transform = 'translateY(0)';
        }, index * 50);
    });

    // Add pulsing effect to important buttons
    const primaryButtons = document.querySelectorAll('.btn-success, .btn-primary');
    primaryButtons.forEach(btn => {
        btn.addEventListener('mouseenter', function() {
            this.style.transform = 'scale(1.05)';
        });
        btn.addEventListener('mouseleave', function() {
            this.style.transform = 'scale(1)';
        });
    });

    // Auto-refresh indicator for game page
    if (window.location.pathname.includes('/Game')) {
        let lastUpdate = Date.now();
        setInterval(() => {
            const elapsed = Math.floor((Date.now() - lastUpdate) / 1000);
            const indicator = document.querySelector('.round-indicator');
            if (indicator) {
                const seconds = 10 - (elapsed % 10);
                indicator.textContent = `Next update in ${seconds}s`;
            }
        }, 1000);
    }

    // Add confetti effect on victory (if victory message exists)
    const victoryAlert = document.querySelector('.alert-success');
    if (victoryAlert && victoryAlert.textContent.includes('MILLIONAIRE')) {
        // Simple celebration effect
        victoryAlert.style.animation = 'pulse 1s infinite';
    }

    console.log('🏠 EstateArchitect initialized successfully!');
});

// Utility function to format currency
function formatCurrency(amount) {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount);
}

// Add page visibility change handler to pause/resume auto-refresh
document.addEventListener('visibilitychange', function() {
    if (document.hidden) {
        console.log('Page hidden - consider pausing auto-updates');
    } else {
        console.log('Page visible - resuming');
    }
});
