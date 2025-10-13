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