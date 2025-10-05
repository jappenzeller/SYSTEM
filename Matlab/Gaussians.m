% Generate various Gaussian curves for reMarkable templates

% Single Gaussian - various standard deviations
figure('Position', [100 100 800 600]);
x = linspace(-5, 5, 1000);

subplot(2,2,1);
hold on;
sigma_values = [0.5, 1, 1.5, 2];
for sigma = sigma_values
    y = (1/(sigma*sqrt(2*pi))) * exp(-0.5*(x/sigma).^2);
    plot(x, y, 'k', 'LineWidth', 2);
end
grid on;
xlabel('x');
ylabel('Probability Density');
title('Standard Deviations: 0.5, 1, 1.5, 2');
set(gca, 'FontSize', 12);
hold off;

% Multiple Gaussians - different means
subplot(2,2,2);
hold on;
mu_values = [-2, 0, 2];
sigma = 1;
for mu = mu_values
    y = (1/(sigma*sqrt(2*pi))) * exp(-0.5*((x-mu)/sigma).^2);
    plot(x, y, 'k', 'LineWidth', 2);
end
grid on;
xlabel('x');
ylabel('Probability Density');
title('Means: -2, 0, 2 (σ=1)');
set(gca, 'FontSize', 12);
hold off;

% Overlapping distributions
subplot(2,2,3);
hold on;
plot(x, normpdf(x, 0, 1), 'k', 'LineWidth', 2);
plot(x, normpdf(x, 1, 1.5), 'k--', 'LineWidth', 2);
grid on;
xlabel('x');
ylabel('Probability Density');
title('Two Overlapping Distributions');
set(gca, 'FontSize', 12);
hold off;

% Blank axes for annotation
subplot(2,2,4);
plot(x, normpdf(x, 0, 1), 'k', 'LineWidth', 2);
grid on;
xlabel('x');
ylabel('Probability Density');
title('Standard Normal Distribution');
set(gca, 'FontSize', 12);

% Save as PNG
print('gaussian_curves_template', '-dpng', '-r300');

% Create individual templates
% Template 1: Clean grid with single standard normal
figure('Position', [100 100 1200 900], 'Color', 'w');
plot(x, normpdf(x, 0, 1), 'k', 'LineWidth', 2.5);
grid on;
xlabel('x', 'FontSize', 14);
ylabel('PDF', 'FontSize', 14);
set(gca, 'FontSize', 12, 'LineWidth', 1.5);
xlim([-4 4]);
ylim([0 0.45]);
print('template_single_gaussian', '-dpng', '-r300');

% Template 2: Multiple curves for comparison
figure('Position', [100 100 1200 900], 'Color', 'w');
hold on;
sigmas = [0.5, 1, 2];
colors = [0 0 0; 0.3 0.3 0.3; 0.6 0.6 0.6];
for i = 1:length(sigmas)
    plot(x, normpdf(x, 0, sigmas(i)), 'Color', colors(i,:), 'LineWidth', 2.5);
end
grid on;
xlabel('x', 'FontSize', 14);
ylabel('PDF', 'FontSize', 14);
legend('σ=0.5', 'σ=1', 'σ=2', 'Location', 'northeast', 'FontSize', 12);
set(gca, 'FontSize', 12, 'LineWidth', 1.5);
xlim([-5 5]);
print('template_multi_gaussian', '-dpng', '-r300');

% Template 3: Blank grid for freehand work
figure('Position', [100 100 1200 900], 'Color', 'w');
plot([-5 5], [0 0], 'k', 'LineWidth', 1.5);  % x-axis
hold on;
plot([0 0], [0 0.5], 'k', 'LineWidth', 1.5);  % y-axis
grid on;
xlabel('x', 'FontSize', 14);
ylabel('PDF', 'FontSize', 14);
set(gca, 'FontSize', 12, 'LineWidth', 1.5);
xlim([-5 5]);
ylim([0 0.5]);
print('template_blank_grid', '-dpng', '-r300');

% Template 4: High-res reference sheet
figure('Position', [100 100 1400 1000], 'Color', 'w');
x_fine = linspace(-6, 6, 2000);

for i = 1:6
    subplot(3,2,i);
    sigma = 0.5 + (i-1)*0.5;  % σ from 0.5 to 3
    y = normpdf(x_fine, 0, sigma);
    plot(x_fine, y, 'k', 'LineWidth', 2);
    grid on;
    xlabel('x');
    ylabel('PDF');
    title(sprintf('σ = %.1f', sigma));
    set(gca, 'FontSize', 11);
    xlim([-6 6]);
end
print('template_reference_sheet', '-dpng', '-r300');

disp('Generated 5 PNG files:');
disp('  gaussian_curves_template.png');
disp('  template_single_gaussian.png');
disp('  template_multi_gaussian.png');
disp('  template_blank_grid.png');
disp('  template_reference_sheet.png');