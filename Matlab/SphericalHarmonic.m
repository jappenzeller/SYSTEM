function visualizeSphericalHarmonic(l, m, radius, resolution)
    % Match your game's coordinate system: +Y = north pole
    % l: degree, m: order, radius: sphere radius (300 in your game)
    % resolution: grid density (e.g., 100)
    
    % Generate theta/phi matching your game convention
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
    surf(X, Y, Z, real(Y_lm));
    axis equal;
    colormap(jet);
    colorbar;
    title(sprintf('Y_{%d}^{%d} on Bloch Sphere', l, m));
end

function Y_lm = computeSphericalHarmonic(l, m, theta, phi)
    % Matches your game's theta/phi convention
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

function [vertices, colors] = generateSphericalHarmonicMesh(l, m, latBands, lonSegments, worldRadius)
    % Match WorldCircuitEmissionController.cs signature
    % latBands, lonSegments: match your Unity mesh resolution
    % worldRadius: 300 in your game
    
    vertexCount = (latBands + 1) * lonSegments;
    vertices = zeros(vertexCount, 3);
    colors = zeros(vertexCount, 1);
    
    index = 1;
    for lat = 0:latBands
        for lon = 0:lonSegments-1
            % Match your game's coordinate calculation exactly
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
    scatter3(vertices(:,1), vertices(:,2), vertices(:,3), 50, colors, 'filled');
    axis equal;
    colormap(jet);
    colorbar;
end

function [vertices, colors] = generateSphericalHarmonicMesh(l, m, latBands, lonSegments, worldRadius)
    % Match WorldCircuitEmissionController.cs signature
    % latBands, lonSegments: match your Unity mesh resolution
    % worldRadius: 300 in your game
    
    vertexCount = (latBands + 1) * lonSegments;
    vertices = zeros(vertexCount, 3);
    colors = zeros(vertexCount, 1);
    
    index = 1;
    for lat = 0:latBands
        for lon = 0:lonSegments-1
            % Match your game's coordinate calculation exactly
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
    scatter3(vertices(:,1), vertices(:,2), vertices(:,3), 50, colors, 'filled');
    axis equal;
    colormap(jet);
    colorbar;
end