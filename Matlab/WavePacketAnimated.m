%% Static Ripple with Count-Based Peak Heights
% Each radial color band height represents the count value
clear; close all;

%% Wave Packet Sample Structure
% Count determines the peak height for each frequency band
samples = struct(...
    'frequency', {0.0, 1.047, 2.094, 3.142, 4.189, 5.236}, ...  % R,Y,G,C,B,M
    'amplitude', {1.0, 1.0, 1.0, 1.0, 1.0, 1.0}, ...             % Always 1 (uniform)
    'phase', {0.0, 0.0, 0.0, 0.0, 0.0, 0.0}, ...                % Always 0 (ignored)
    'count', {20, 5, 30, 10, 40, 15} ...                         % This determines height!
);

% Test cases - uncomment to try different compositions
% % Pure red (high count)
% samples.count = {50, 0, 0, 0, 0, 0};

% % RGB mix
% samples.count = {20, 0, 30, 0, 40, 0};

% % Even distribution
% samples.count = {20, 20, 20, 20, 20, 20};

%% Parameters
% Grid parameters
nx = 256;
ny = 256;
grid_range = 20;
x_range = [-grid_range, grid_range];
y_range = [-grid_range, grid_range];

% Ripple parameters
ripple_frequency = 3;      % Number of ripples across radius
max_radius = 18;
count_scale = 0.03;        % Scale factor for count to height conversion

% Animation parameters
n_frames = 120;
modulation_depth = 0.1;    % ±10% oscillation
oscillation_rates = [1.0, 1.2, 0.8, 1.4, 0.9, 1.1];

%% Create spatial grid
x = linspace(x_range(1), x_range(2), nx);
y = linspace(y_range(1), y_range(2), ny);
[X, Y] = meshgrid(x, y);
R = sqrt(X.^2 + Y.^2);

%% Helper function for radius to frequency and count mapping
function [freq, count_height, rgb] = radius_to_frequency_count(r, samples, max_r, count_scale)
    r_norm = min(r / max_r, 1);
    freq = (1 - r_norm) * 6.283;
    
    sample_freqs = [samples.frequency];
    sample_counts = [samples.count];
    
    % Scale counts to reasonable height values
    scaled_counts = sample_counts * count_scale;
    
    extended_freqs = [sample_freqs, 6.283];
    extended_counts = [scaled_counts, scaled_counts(1)];  % Wrap to red
    
    idx = find(extended_freqs <= freq, 1, 'last');
    if isempty(idx), idx = 1; end
    
    % Interpolate count-based height
    if idx < length(extended_freqs)
        t = (freq - extended_freqs(idx)) / (extended_freqs(idx+1) - extended_freqs(idx));
        count_height = extended_counts(idx) * (1-t) + extended_counts(idx+1) * t;
    else
        count_height = extended_counts(end);
    end
    
    % RGB color mapping
    freq_points = [0.0, 1.047, 2.094, 3.142, 4.189, 5.236, 6.283];
    colors = [
        1, 0, 0;      % Red
        1, 1, 0;      % Yellow  
        0, 1, 0;      % Green
        0, 1, 1;      % Cyan
        0, 0, 1;      % Blue
        1, 0, 1;      % Magenta
        1, 0, 0;      % Red (wrap)
    ];
    
    color_idx = find(freq_points <= freq, 1, 'last');
    if isempty(color_idx), color_idx = 1; end
    
    if color_idx < length(freq_points)
        ct = (freq - freq_points(color_idx)) / (freq_points(color_idx+1) - freq_points(color_idx));
        rgb = colors(color_idx,:) * (1-ct) + colors(color_idx+1,:) * ct;
    else
        rgb = colors(end,:);
    end
end

%% Pre-calculate static color field and base heights
color_field = zeros(nx, ny, 3);
base_height_field = zeros(nx, ny);

for i = 1:nx
    for j = 1:ny
        r = R(i,j);
        [~, count_height, rgb] = radius_to_frequency_count(r, samples, max_radius, count_scale);
        color_field(i,j,:) = rgb;
        
        % Create ripple with count-based amplitude
        ripple = (1 + cos(2 * pi * ripple_frequency * r / max_radius)) / 2;
        base_height_field(i,j) = count_height * ripple;
    end
end

%% Store original counts
original_counts = [samples.count];

%% Static visualization first
figure('Position', [50, 50, 1400, 700]);

% 3D view
subplot(1,3,[1,2]);
surf(X, Y, base_height_field, color_field, 'EdgeColor', 'none', 'FaceColor', 'interp');
view(35, 20);
xlabel('x'); ylabel('y'); zlabel('Height (Count)');
title('Static Ripple - Height Represents Count');
lighting gouraud;
light('Position', [1, 1, 3]);
light('Position', [-1, -1, 1]);
axis tight;
daspect([1 1 0.3]);
grid on;

% Count bar chart
subplot(1,3,3);
freq_labels = {'Red', 'Yellow', 'Green', 'Cyan', 'Blue', 'Magenta'};
colors = [1 0 0; 1 1 0; 0 1 0; 0 1 1; 0 0 1; 1 0 1];
hold on;
for s = 1:length(samples)
    bar(s, samples(s).count, 0.8, 'FaceColor', colors(s,:), 'EdgeColor', 'none');
end
xlabel('Frequency Band');
ylabel('Count');
title('Input Count Values');
set(gca, 'XTick', 1:6, 'XTickLabel', freq_labels);
grid on;

%% Animation with oscillating counts
figure('Position', [50, 50, 1200, 600]);

for frame = 1:n_frames
    t = frame * 2*pi / n_frames;
    
    % Oscillate each count independently
    for s = 1:length(samples)
        oscillation = 1 + modulation_depth * sin(oscillation_rates(s) * t);
        samples(s).count = original_counts(s) * oscillation;
    end
    
    % Recalculate height field with modulated counts
    height_field = zeros(size(R));
    for i = 1:nx
        for j = 1:ny
            r = R(i,j);
            [~, count_height, ~] = radius_to_frequency_count(r, samples, max_radius, count_scale);
            
            ripple = (1 + cos(2 * pi * ripple_frequency * r / max_radius)) / 2;
            height_field(i,j) = count_height * ripple;
        end
    end
    
    clf;
    
    % Main 3D visualization
    subplot(1,2,1);
    surf(X, Y, height_field, color_field, 'EdgeColor', 'none', 'FaceColor', 'interp');
    view(35, 20);
    xlabel('x'); ylabel('y'); zlabel('Height');
    title(sprintf('Count-Based Ripple Animation - Frame %d/%d', frame, n_frames));
    lighting gouraud;
    light('Position', [1, 1, 3]);
    light('Position', [-1, -1, 1]);
    axis([-grid_range grid_range -grid_range grid_range 0 max(original_counts)*count_scale*1.5]);
    daspect([1 1 0.3]);
    grid on;
    
    % Count indicator panel
    subplot(1,2,2);
    hold on;
    
    for s = 1:length(samples)
        current_count = samples(s).count;
        base_count = original_counts(s);
        
        % Draw base count as line
        plot([s-0.3, s+0.3], [base_count, base_count], 'k--', 'LineWidth', 1);
        
        % Draw current count as colored bar
        bar(s, current_count, 0.6, 'FaceColor', colors(s,:), 'EdgeColor', 'none');
        
        % Show count value
        text(s, current_count + 2, sprintf('%.0f', current_count), ...
             'HorizontalAlignment', 'center', 'FontSize', 9);
    end
    
    xlabel('Frequency Band');
    ylabel('Count');
    title('Real-time Count Values (±10%)');
    set(gca, 'XTick', 1:6, 'XTickLabel', freq_labels);
    ylim([0, max(original_counts) * 1.3]);
    grid on;
    
    drawnow;
end

% Reset counts
for s = 1:length(samples)
    samples(s).count = original_counts(s);
end

%% Create radial profile visualization
figure('Position', [100, 100, 1000, 400]);

% Calculate radial profile
radial_dist = linspace(0, max_radius, 200);
radial_heights = zeros(size(radial_dist));
radial_colors = zeros(length(radial_dist), 3);
radial_counts = zeros(size(radial_dist));

for k = 1:length(radial_dist)
    r = radial_dist(k);
    [~, count_height, rgb] = radius_to_frequency_count(r, samples, max_radius, count_scale);
    ripple = (1 + cos(2 * pi * ripple_frequency * r / max_radius)) / 2;
    radial_heights(k) = count_height * ripple;
    radial_colors(k,:) = rgb;
    radial_counts(k) = count_height / count_scale;  % Get actual count value
end

% Plot radial cross-section
subplot(1,2,1);
hold on;
for k = 1:length(radial_dist)
    plot(radial_dist(k), radial_heights(k), 'o', ...
         'Color', radial_colors(k,:), ...
         'MarkerFaceColor', radial_colors(k,:), ...
         'MarkerSize', 3);
end
xlabel('Distance from Center');
ylabel('Height');
title('Radial Cross-Section (Height = Count × Ripple)');
xlim([0, max_radius]);
grid on;

% Plot count distribution
subplot(1,2,2);
hold on;
for k = 1:length(radial_dist)
    plot(radial_dist(k), radial_counts(k), 'o', ...
         'Color', radial_colors(k,:), ...
         'MarkerFaceColor', radial_colors(k,:), ...
         'MarkerSize', 3);
end
xlabel('Distance from Center');
ylabel('Count Value');
title('Count Distribution Across Radius');
xlim([0, max_radius]);
ylim([0, max(original_counts)*1.2]);
grid on;

% Add frequency band labels
freq_positions = (1 - [samples.frequency]/6.283) * max_radius;
for s = 1:length(samples)
    if samples(s).count > 0
        text(freq_positions(s), samples(s).count + 2, freq_labels{s}, ...
             'HorizontalAlignment', 'center', 'Color', colors(s,:), 'FontWeight', 'bold');
    end
end

fprintf('Visualization complete. Height peaks represent count values for each frequency.\n');