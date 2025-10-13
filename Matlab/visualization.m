%% Spherical Harmonics Visualization Suite
% Complete MATLAB code for visualizing spherical harmonics with interactive controls
% Matches SYSTEM game's coordinate conventions: +Y = north pole, theta from +Y, phi in XZ plane

%% Main Visualization Functions

function visualizeSphericalHarmonic(l, m, radius, resolution)
    % Static visualization matching game's coordinate system
    % l: degree, m: order, radius: sphere radius (300 in game)
    % resolution: grid density (e.g., 100)
    
    % Generate theta/phi matching game convention
    theta = linspace(0, pi, resolution);      % From +Y axis (north pole)
    phi = linspace(0, 2*pi, resolution);      % In XZ plane
    [THETA, PHI] = meshgrid(theta, phi);
    
    % Compute spherical harmonic
    Y_lm = computeSphericalHarmonic(l, m, THETA, PHI);
    
    % Convert to Cartesian (matching Unity's coordinate system)
    X = radius * sin(THETA) .* cos(PHI);
    Y = radius * cos(THETA);              % +Y is north pole
    Z = radius * sin(THETA) .* sin(PHI);
    
    % Visualize with color mapping
    figure('Name', sprintf('Y_{%d}^{%d} Static', l, m));
    surf(X, Y, Z, real(Y_lm));
    axis equal;
    colormap(jet);
    colorbar;
    title(sprintf('Y_{%d}^{%d} on Bloch Sphere', l, m));
    xlabel('X'); ylabel('Y'); zlabel('Z');
    shading interp;
    camlight; lighting gouraud;
end

function animateSphericalHarmonic(l, m, worldRadius, fps)
    % Animate single harmonic with interactive controls
    
    resolution = 60;
    
    % Grid setup
    theta = linspace(0, pi, resolution);
    phi = linspace(0, 2*pi, resolution);
    [THETA, PHI] = meshgrid(theta, phi);
    
    X = worldRadius * sin(THETA) .* cos(PHI);
    Y = worldRadius * cos(THETA);
    Z = worldRadius * sin(THETA) .* sin(PHI);
    
    % Compute harmonic
    Y_lm = computeSphericalHarmonic(l, m, THETA, PHI);
    max_val = max(abs(Y_lm(:)));
    
    % Figure with controls
    fig = figure('Position', [100, 100, 900, 800], ...
                 'Name', sprintf('Y_{%d}^{%d} Animation', l, m), ...
                 'NumberTitle', 'off');
    
    ax = axes('Position', [0.1, 0.15, 0.85, 0.8]);
    
    % Animation data
    animData = struct();
    animData.X = X;
    animData.Y = Y; 
    animData.Z = Z;
    animData.Y_lm = Y_lm;
    animData.max_val = max_val;
    animData.worldRadius = worldRadius;
    animData.phase = 0;
    animData.fps = fps;
    animData.isPlaying = false;
    animData.ax = ax;
    animData.l = l;
    animData.m = m;
    
    fig.UserData = animData;
    
    % Control panel
    controlPanel = uipanel('Position', [0, 0, 1, 0.1]);
    
    % Buttons
    playBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                       'String', '▶ Play', ...
                       'Units', 'normalized', ...
                       'Position', [0.02, 0.2, 0.08, 0.6], ...
                       'Callback', @(src, evt) playCallbackSingle(fig, src));
    
    pauseBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                        'String', '❚❚ Pause', ...
                        'Units', 'normalized', ...
                        'Position', [0.11, 0.2, 0.08, 0.6], ...
                        'Callback', @(src, evt) pauseCallbackSingle(fig), ...
                        'Enable', 'off');
    
    restartBtn = uicontrol(controlPanel, 'Style', 'pushbutton', ...
                          'String', '↺ Restart', ...
                          'Units', 'normalized', ...
                          'Position', [0.2, 0.2, 0.08, 0.6], ...
                          'Callback', @(src, evt) restartHarmonic(fig));
    
    % Display mode toggle
    modeBtn = uicontrol(controlPanel, 'Style', 'togglebutton', ...
                       'String', 'Real Part', ...
                       'Units', 'normalized', ...
                       'Position', [0.35, 0.2, 0.1, 0.6], ...
                       'Callback', @(src, evt) modeCallback(fig, src));
    
    % Phase slider
    phaseSlider = uicontrol(controlPanel, 'Style', 'slider', ...
                           'Min', 0, 'Max', 2*pi, 'Value', 0, ...
                           'Units', 'normalized', ...
                           'Position', [0.5, 0.2, 0.3, 0.6], ...
                           'Callback', @(src, evt) phaseCallback(fig, src));
    
    uicontrol(controlPanel, 'Style', 'text', ...
              'String', 'Phase:', ...
              'Units', 'normalized', ...
              'Position', [0.5, 0.7, 0.1, 0.2]);
    
    phaseText = uicontrol(controlPanel, 'Style', 'text', ...
                         'String', '0°', ...
                         'Units', 'normalized', ...
                         'Position', [0.7, 0.7, 0.1, 0.2]);
    
    % Store controls
    animData.playBtn = playBtn;
    animData.pauseBtn = pauseBtn;
    animData.phaseSlider = phaseSlider;
    animData.phaseText = phaseText;
    animData.modeBtn = modeBtn;
    animData.displayMode = 'real';  % 'real', 'imag', or 'abs'
    fig.UserData = animData;
    
    % Timer
    animTimer = timer('ExecutionMode', 'fixedRate', ...
                     'Period', 1/fps, ...
                     'TimerFcn', @(src, evt) updateHarmonic(fig));
    
    animData.timer = animTimer;
    fig.UserData = animData;
    
    % Initial draw
    updateHarmonic(fig);
    
    % Cleanup
    fig.CloseRequestFcn = @(src, evt) closeAnimationSingle(src, animTimer);
end

function animateQuantumState(l_values, m_values, coefficients, worldRadius, fps)
    % Animate superposition of multiple harmonics with controls
    
    resolution = 50;
    duration = 5;  % seconds
    
    % Pre-generate grid
    theta = linspace(0, pi, resolution);
    phi = linspace(0, 2*pi, resolution);
    [THETA, PHI] = meshgrid(theta, phi);
    
    % Cartesian coordinates
    X = worldRadius * sin(THETA) .* cos(PHI);
    Y = worldRadius * cos(THETA);
    Z = worldRadius * sin(THETA) .* sin(PHI);
    
    % Set up figure with controls
    fig = figure('Position', [100, 100, 900, 800], ...
                 'Name', 'Quantum State Animation', ...
                 'NumberTitle', 'off');
    
    % Create axes for 3D plot
    ax = axes('Position', [0.1, 0.15, 0.85, 0.8]);
    
    % Animation data structure
    animData = struct();
    animData.l_values = l_values;
    animData.m_values = m_values;
    animData.coefficients = coefficients;
    animData.X = X;
    animData.Y = Y;
    animData.Z = Z;
    animData.THETA = THETA;
    animData.PHI = PHI;
    animData.worldRadius = worldRadius;
    animData.currentTime = 0;
    animData.duration = duration;
    animData.fps = fps;
    animData.isPlaying = false;
    animData.ax = ax;
    
    % Store in figure
    fig.UserData = animData;
    
    % Create control buttons
    playBtn = uicontrol('Style', 'pushbutton', ...
                        'String', 'Play', ...
                        'Position', [20, 20, 60, 30], ...
                        'Callback', @(src, evt) playCallback(fig, src));
    
    pauseBtn = uicontrol('Style', 'pushbutton', ...
                         'String', 'Pause', ...
                         'Position', [90, 20, 60, 30], ...
                         'Callback', @(src, evt) pauseCallback(fig), ...
                         'Enable', 'off');
    
    restartBtn = uicontrol('Style', 'pushbutton', ...
                           'String', 'Restart', ...
                           'Position', [160, 20, 60, 30], ...
                           'Callback', @(src, evt) restartCallback(fig));
    
    % Speed control slider
    speedSlider = uicontrol('Style', 'slider', ...
                           'Min', 0.1, 'Max', 3, 'Value', 1, ...
                           'Position', [250, 20, 150, 30], ...
                           'Callback', @(src, evt) speedCallback(fig, src));
    
    uicontrol('Style', 'text', ...
              'String', 'Speed:', ...
              'Position', [250, 50, 50, 20]);
    
    speedText = uicontrol('Style', 'text', ...
                         'String', '1.0x', ...
                         'Position', [350, 50, 50, 20]);
    
    % Store UI elements
    animData.playBtn = playBtn;
    animData.pauseBtn = pauseBtn;
    animData.speedSlider = speedSlider;
    animData.speedText = speedText;
    animData.speedMultiplier = 1;
    fig.UserData = animData;
    
    % Create timer
    animTimer = timer('ExecutionMode', 'fixedRate', ...
                     'Period', 1/fps, ...
                     'TimerFcn', @(src, evt) updateAnimation(fig), ...
                     'StopFcn', @(src, evt) stopAnimation(fig));
    
    animData.timer = animTimer;
    fig.UserData = animData;
    
    % Initial draw
    updateAnimation(fig);
    
    % Clean up timer when figure closes
    fig.CloseRequestFcn = @(src, evt) closeAnimation(src, animTimer);
end

function [vertices, colors] = generateSphericalHarmonicMesh(l, m, latBands, lonSegments, worldRadius)
    % Generate mesh matching Unity's WorldCircuitEmissionController pattern
    % latBands, lonSegments: match Unity mesh resolution
    % worldRadius: 300 in game
    
    vertexCount = (latBands + 1) * lonSegments;
    vertices = zeros(vertexCount, 3);
    colors = zeros(vertexCount, 1);
    
    index = 1;
    for lat = 0:latBands
        for lon = 0:lonSegments-1
            % Match game's coordinate calculation exactly
            theta = pi * (lat + 0.5) / latBands;  % [0, π] from +Y axis
            phi = 2 * pi * lon / lonSegments;     % [0, 2π] in XZ plane
            
            % Cartesian position
            x = worldRadius * sin(theta) * cos(phi);
            y = worldRadius * cos(theta);
            z = worldRadius * sin(theta) * sin(phi);
            
            vertices(index, :) = [x, y, z];
            
            % Compute harmonic value at this point
            Y_lm = computeSphericalHarmonic(l, m, theta, phi);
            colors(index) = real(Y_lm);  % Or abs() for magnitude
            
            index = index + 1;
        end
    end
    
    % Visualize
    figure('Name', sprintf('Y_{%d}^{%d} Mesh', l, m));
    scatter3(vertices(:,1), vertices(:,2), vertices(:,3), 50, colors, 'filled');
    axis equal;
    colormap(jet);
    colorbar;
    title(sprintf('Y_{%d}^{%d} on Sphere Mesh', l, m));
    xlabel('X'); ylabel('Y'); zlabel('Z');
end

%% Callback Functions for Single Harmonic Animation

function updateHarmonic(fig)
    if ~isvalid(fig)
        return;
    end
    
    data = fig.UserData;
    
    % Calculate current value based on mode
    switch data.displayMode
        case 'real'
            current_value = real(data.Y_lm * exp(1i * data.phase));
            cmap = redblue(256);
            clims = [-data.max_val, data.max_val];
        case 'imag'
            current_value = imag(data.Y_lm * exp(1i * data.phase));
            cmap = redblue(256);
            clims = [-data.max_val, data.max_val];
        case 'abs'
            current_value = abs(data.Y_lm);
            cmap = 'jet';
            clims = [0, data.max_val];
    end
    
    % Update surface
    cla(data.ax);
    surf(data.ax, data.X, data.Y, data.Z, current_value, 'EdgeColor', 'none');
    
    % Formatting
    axis(data.ax, 'equal');
    axis(data.ax, [-data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius] * 1.2);
    shading(data.ax, 'interp');
    colormap(data.ax, cmap);
    caxis(data.ax, clims);
    colorbar(data.ax);
    
    camlight;
    lighting gouraud;
    view(data.ax, 45, 30);
    
    title(data.ax, sprintf('Y_{%d}^{%d} - %s - Phase: %.0f°', ...
          data.l, data.m, data.displayMode, data.phase * 180/pi));
    
    % Update phase if playing
    if data.isPlaying
        data.phase = mod(data.phase + 0.1, 2*pi);
        data.phaseSlider.Value = data.phase;
        data.phaseText.String = sprintf('%.0f°', data.phase * 180/pi);
        fig.UserData = data;
    end
    
    drawnow;
end

function playCallbackSingle(fig, btn)
    data = fig.UserData;
    if ~data.isPlaying
        data.isPlaying = true;
        btn.Enable = 'off';
        data.pauseBtn.Enable = 'on';
        fig.UserData = data;
        start(data.timer);
    end
end

function pauseCallbackSingle(fig)
    data = fig.UserData;
    if data.isPlaying
        data.isPlaying = false;
        stop(data.timer);
        data.playBtn.Enable = 'on';
        data.pauseBtn.Enable = 'off';
        fig.UserData = data;
    end
end

function restartHarmonic(fig)
    data = fig.UserData;
    data.phase = 0;
    data.phaseSlider.Value = 0;
    data.phaseText.String = '0°';
    fig.UserData = data;
    updateHarmonic(fig);
end

function modeCallback(fig, btn)
    data = fig.UserData;
    if btn.Value
        btn.String = 'Imag Part';
        data.displayMode = 'imag';
    else
        btn.String = 'Real Part';
        data.displayMode = 'real';
    end
    fig.UserData = data;
    updateHarmonic(fig);
end

function phaseCallback(fig, slider)
    data = fig.UserData;
    data.phase = slider.Value;
    data.phaseText.String = sprintf('%.0f°', data.phase * 180/pi);
    fig.UserData = data;
    updateHarmonic(fig);
end

function closeAnimationSingle(fig, timer)
    if isvalid(timer)
        stop(timer);
        delete(timer);
    end
    delete(fig);
end

%% Callback Functions for Quantum State Animation

function updateAnimation(fig)
    if ~isvalid(fig)
        return;
    end
    
    data = fig.UserData;
    t = data.currentTime;
    
    % Compute superposition
    psi = zeros(size(data.THETA));
    for k = 1:length(data.l_values)
        Y_lm = computeSphericalHarmonic(data.l_values(k), data.m_values(k), ...
                                       data.THETA, data.PHI);
        omega = getEnergy(data.l_values(k), data.m_values(k));
        phase = exp(-1i * omega * t);
        psi = psi + data.coefficients(k) * Y_lm * phase;
    end
    
    % Clear and redraw
    cla(data.ax);
    
    % Plot surface
    surf(data.ax, data.X, data.Y, data.Z, abs(psi).^2, 'EdgeColor', 'none');
    
    % Formatting
    axis(data.ax, 'equal');
    axis(data.ax, [-data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius ...
                   -data.worldRadius data.worldRadius] * 1.2);
    shading(data.ax, 'interp');
    colormap(data.ax, 'jet');
    
    % Fixed color scale
    maxVal = 0;
    for k = 1:length(data.l_values)
        maxVal = maxVal + abs(data.coefficients(k));
    end
    caxis(data.ax, [0 maxVal^2]);
    colorbar(data.ax);
    
    % Lighting and view
    camlight;
    lighting gouraud;
    view(data.ax, 30 + t*20, 30);
    
    % Title
    title(data.ax, sprintf('Quantum State: t = %.2f / %.2f', t, data.duration));
    xlabel(data.ax, 'X'); 
    ylabel(data.ax, 'Y'); 
    zlabel(data.ax, 'Z');
    
    % Update time
    if data.isPlaying
        data.currentTime = data.currentTime + (1/data.fps) * data.speedMultiplier;
        if data.currentTime > data.duration
            data.currentTime = data.currentTime - data.duration;  % Loop
        end
        fig.UserData = data;
    end
    
    drawnow;
end

function playCallback(fig, btn)
    data = fig.UserData;
    if ~data.isPlaying
        data.isPlaying = true;
        btn.Enable = 'off';
        data.pauseBtn.Enable = 'on';
        fig.UserData = data;
        start(data.timer);
    end
end

function pauseCallback(fig)
    data = fig.UserData;
    if data.isPlaying
        data.isPlaying = false;
        stop(data.timer);
        data.playBtn.Enable = 'on';
        data.pauseBtn.Enable = 'off';
        fig.UserData = data;
    end
end

function restartCallback(fig)
    data = fig.UserData;
    data.currentTime = 0;
    fig.UserData = data;
    updateAnimation(fig);
end

function speedCallback(fig, slider)
    data = fig.UserData;
    data.speedMultiplier = slider.Value;
    data.speedText.String = sprintf('%.1fx', slider.Value);
    fig.UserData = data;
end

function closeAnimation(fig, timer)
    if isvalid(timer)
        stop(timer);
        delete(timer);
    end
    delete(fig);
end

function stopAnimation(fig)
    % Timer stop callback (does nothing currently)
end

%% Core Spherical Harmonic Function

function Y_lm = computeSphericalHarmonic(l, m, theta, phi)
    % Compute spherical harmonic using game's theta/phi convention
    % theta: [0, π] from +Y axis (north pole)
    % phi: [0, 2π] in XZ plane
    
    P_lm = legendre(l, cos(theta));
    if m >= 0
        P_lm = squeeze(P_lm(m+1, :, :));
    else
        P_lm = squeeze(P_lm(-m+1, :, :));
        P_lm = P_lm * (-1)^(-m) * factorial(l+m) / factorial(l-m);
    end
    Y_lm = P_lm .* exp(1i * m * phi);
    
    % Normalization
    norm_factor = sqrt((2*l+1) * factorial(l-m) / (4*pi * factorial(l+m)));
    Y_lm = norm_factor * Y_lm;
end

function E = getEnergy(l, m)
    % Energy eigenvalue (arbitrary units for visualization)
    % Different energies create different oscillation frequencies
    E = (l + abs(m)) * 2 * pi / 5;  % Scaled for visible oscillation
end

%% Helper Functions

function cmap = redblue(n)
    % Red-white-blue colormap for real/imaginary parts
    r = [linspace(0, 1, n/2), ones(1, n/2)]';
    g = [linspace(0, 1, n/2), linspace(1, 0, n/2)]';
    b = [ones(1, n/2), linspace(1, 0, n/2)]';
    cmap = [r g b];
end

%% Example Usage

% Match your world parameters
WORLD_RADIUS = 300;
LAT_BANDS = 30;     % From your emission controller
LON_SEGMENTS = 30;

% Static visualization
% visualizeSphericalHarmonic(2, 1, WORLD_RADIUS, 100);

% Interactive single harmonic
% animateSphericalHarmonic(3, 2, WORLD_RADIUS, 30);

% Generate mesh data like Unity
% [verts, cols] = generateSphericalHarmonicMesh(3, 2, LAT_BANDS, LON_SEGMENTS, WORLD_RADIUS);

% Animate quantum superposition with controls
% animateQuantumState([2, 3], [0, 1], [0.7, 0.3*exp(1i*pi/4)], WORLD_RADIUS, 30);

% Create interference pattern
% animateQuantumState([3, 3], [1, 3], [1, 0.8*exp(1i*pi/3)], WORLD_RADIUS, 30);